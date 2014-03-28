using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MobilEMS.Data
{
   public partial class Vehicle
   {
      #region Maintenance Data & PM Report Management

      // Implementation Notes
      //
      // This partial class for Vehicle implements data management for maintenance data.
      //
      // Each end user's phone will report status of a vehicle's planned maintenance
      // properties each time it connects to the MobilEMS server.  This is expected to
      // present many more data reports than are needed to manage the vehicle's status.
      // The purpose of this code is to limit the number of PM reports stored in a
      // vehicle's history.
      //
      // Two methods are used:
      //
      // MaintenanceDataAddPmReport: Add PMReport objects to the Vehicle's MaintenanceData
      // property.  This manages all aspects of cache and history.
      //
      // MaintenanceDataCacheFlush: Flush the "cached" PMReport (if it exists) to history.
      //
      // A simple study was done that showed caching MaintenanceDataLatestHistoryReport
      // improved performance speed in large data adds (like 10 years of data) by a factor of
      // three to four times over the same code without the cache.  Caching the
      // MaintenanceDataCacheReport by itself or with MaintenanceDataLatestHistoryReport
      // showed little benefit, so no caching is used for MaintenanceDataCacheReport
      // in favor of simplicity in the code and a more direct design.
      //
      // SPECIAL NOTE:
      //
      // All data changes happen in memory - this code does not store data back to the data store.
      // It is assumed the caller (usually a controller) holds the database context, owns the
      // process of reading and storing data, and delivers a Vehicle object with the MaintenanceData
      // property initialized and populated with all available data.
      //

      #region Maintenance Data Properties

      /// <summary>
      /// Return PMReport from "cache" - returns null if no cached PMReport
      /// There should be at most only one cached report - throws exception otherwise
      /// </summary>
      /// 
      public PMReport MaintenanceDataCacheReport
      {
         get
         {
            try
            {
               return MaintenanceData.SingleOrDefault(x => !x.Visible);
            }
            catch (Exception ex)
            {
               //_maintenanceDataCacheReport = null;
               string errorReports = MaintenanceData
                  .Where(x => !x.Visible)
                  .Aggregate(string.Empty, (current, pmReport) => current + ("PMReport: " + pmReport.ToString() + Environment.NewLine));
               throw new AmbiguousMatchException(
                  "More than one cached (not Visible) report present in Maintenance data for vehicle Id [" + Id + "]" +
                                Environment.NewLine + Environment.NewLine + errorReports, ex);
            }
         }
      }

      /// <summary>
      /// Return PMReport with latest date (including cached report)
      /// </summary>
      public PMReport MaintenanceDataLatestReport
      {
         get { return MaintenanceData.OrderBy(x => x.Time).LastOrDefault(); }
      }

      public List<PMReport> MaintenanceDataHistoryReports 
      {
         get { return MaintenanceData.Where(x => x.Visible).OrderBy(x => x.Time).ToList(); }
      }

      /// <summary>
      /// Return PMReport with latest date among visible (not cache) reports
      /// </summary>
      public PMReport MaintenanceDataLatestHistoryReport
      {
         get
         {
            return _maintenanceDataLatestHistoryReport ??
               (_maintenanceDataLatestHistoryReport =
                      MaintenanceData.Where(x => x.Visible).OrderBy(x => x.Time).LastOrDefault());
         }
         private set { _maintenanceDataLatestHistoryReport = value; }
      }

      private PMReport _maintenanceDataLatestHistoryReport;

      public static Expression<Func<Vehicle, DateTime?>> LastPmDataTimeExpression()
      {
         return (a => a.MaintenanceData
            .Where(x => x.Visible)
            .OrderBy(x=>x.Time)
            .LastOrDefault().Time);
      }

      public DateTime? LastPmDataTime
      {
         get { return (MaintenanceDataLatestHistoryReport != null ? MaintenanceDataLatestHistoryReport.Time : (DateTime?) null); }
      }

      public string PmUpdateStatus 
      {
         get { return MaintenanceDataCacheReport == null ? String.Empty : "Update Available"; }
      }
      
      #endregion

      #region Maintenance Data Methods


       public PMReport MaintenanceDataHistoryReportbyDate(int reportId)
       {
           return MaintenanceData.FirstOrDefault(x => x.Id == reportId);
       }

      /// <summary>
      /// MaintenanceDataAddPmReport
      /// 
      /// Attempt to add a new PMReport to the MaintenanceData for a vehicle.
      /// 
      /// If no prior history exists, add the new report as history.
      /// 
      /// If new report is older than the newest report (history or cache)
      /// take no action and return false.
      /// </summary>
      /// <param name="newPmReport">PMReport object to be added to history</param>
      /// <param name="pmReportSet">Data set from which PMReports must be deleted if needed</param>
      /// <returns>bool - True - new report added; False - new report not added</returns>
      //public bool MaintenanceDataAddPmReport(MobilEMSContextContainer db, PMReport newPmReport)
      public bool MaintenanceDataAddPmReport(IDbSet<PMReport> pmReportSet, PMReport newPmReport)
      {
         if (newPmReport == null)
            return false;

         // If there is no existing history, the new report becomes the start of history
         // and we're done
         if (MaintenanceData.Count == 0)
         {
            newPmReport.Visible = true;
            MaintenanceData.Add(newPmReport);
            MaintenanceDataLatestHistoryReport = newPmReport;
            return true;
         }

         // Some history exists - compare new report to cache and history and decide
         // what to do with it - get the existing reports
         PMReport maintenanceDataCacheReport = MaintenanceDataCacheReport;
         PMReport maintenanceDataLatestHistoryReport = MaintenanceDataLatestHistoryReport;
         PMReport workReport = maintenanceDataCacheReport ?? maintenanceDataLatestHistoryReport;

         // If we don't have cache or history, it is a problem but should never happen.
         // Whichever report (cache or latest history) we ended up with, if the new
         // report is older than it, we're done
         if ((workReport == null) || (workReport.Time >= newPmReport.Time))
            return false;

         // New report is going to be added - remove cache if it is present
         if (maintenanceDataCacheReport != null)
         {
            MaintenanceData.Remove(maintenanceDataCacheReport); // remove old report from list of maintenance data
            pmReportSet.Remove(maintenanceDataCacheReport);   // remove the actual report entity (and associated values via cascade delete)
         }

         // Is new report "cache" or "history"?
         bool historyOverLimit = maintenanceDataLatestHistoryReport != null && maintenanceDataLatestHistoryReport.HasPropertyOverLimit;
         bool newOverLimit = newPmReport.HasPropertyOverLimit;

         // If history has an overlimit condition we accumulate data daily, otherwise weekly
         int cacheLifeSpanDays = historyOverLimit ? 1 : 7;

         // Is it newer than the time of the latest history report + cache life span
         // or
         //    Does it show a new problem (new oeverlimit and history not overlimit)
         //    or
         //    Does it show a problem was corrected (new not overlimit and history overlimit)
         newPmReport.Visible =
            (maintenanceDataLatestHistoryReport != null
             && newPmReport.Time >= maintenanceDataLatestHistoryReport.Time.AddDays(cacheLifeSpanDays))
            || ((newOverLimit && !historyOverLimit) | (!newOverLimit && historyOverLimit));

         MaintenanceData.Add(newPmReport);
         if (newPmReport.Visible)
            MaintenanceDataLatestHistoryReport = newPmReport;

         return true;

      }

      /// <summary>
      /// Convert the cached PMReport for a vehicle to history - flush the cache
      /// </summary>
      /// <returns>
      /// True - Cache converted - DB context SaveChanges needed to save changes
      /// False - No actions taken - No DB context SaveChanges needed
      /// </returns>
      public bool MaintenanceDataCacheFlush()
      {
         PMReport pmReport = MaintenanceDataCacheReport;
         if (pmReport == null)
            return false;

         MaintenanceData.Remove(pmReport);
         pmReport.Visible = true;
         MaintenanceData.Add(pmReport);
         MaintenanceDataLatestHistoryReport = pmReport;
         return true;
      }
      #endregion
      #endregion
   }
}
