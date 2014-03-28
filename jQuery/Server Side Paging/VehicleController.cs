using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.SessionState;
using Halcyon.Data;
using MobilEMS.Data;
using MobilEMS.Web.Models;
using MobilEMS.Web.Utility;

namespace MobilEMS.Web.API
{
   [Authorize(Roles = MobilEMSApp.HortonAdministratorRole
      + "," + MobilEMSApp.AgencyAdministratorRole
      + "," + MobilEMSApp.AgencyFleetManagerRole)]
   public class VehicleController : ApiController
   {
      #region Class Variables, Constructors & Dispose

      private MobilEMSContextContainer db = new MobilEMSContextContainer();

      public RolesWrapper Roles
      {
         get { return _roles; }
         set { _roles = value; }
      }
      private RolesWrapper _roles = new RolesWrapper();

      protected override void Dispose(bool disposing)
      {
         if (disposing)
         {
            db.Dispose();
         }
         base.Dispose(disposing);
      }

      #endregion

      [HttpGet]
      public void SetBrowserTimeZoneOffset(double offset)
      {
         BrowserTimeZoneOffset = -1 * offset;
      }

      internal static double BrowserTimeZoneOffset
      {
         get
         {
            return HttpContext.Current.Session["BrowserTimeZoneOffset"] == null
               ? 0
               : (double)HttpContext.Current.Session["BrowserTimeZoneOffset"];
         }
         set { HttpContext.Current.Session["BrowserTimeZoneOffset"] = value; }
      }

      [HttpPost]
      public DataTableResults GetVehicleTableData(DataTableParams dtParams)
      {
         HttpContextWrapper wrapper = (HttpContextWrapper) Request.Properties["MS_HttpContext"];
         NameValueCollection paramCollection = wrapper.Request.Params;

         // If a list of vehicle Ids was passed to us and the "bFlushCache" boolean is true
         // (indicating the user actually presed the "Requets Maintenance Data" button), we
         // flush the MaintenanceData cache, making the cached PMReport part of the
         // vehicle's MaintenanceData history.
         //
         // If the bFlushCache indicator is false, we build a list of vehicle Id's so we
         // can "check" the checkboxes on the output lines (way at the bottom of this method).
         bool bFlushCache = paramCollection["bFlushCache"] == "true";

         string[] vehicleIdstrings = paramCollection.GetValues("vehicleIds");
         List<int> vehicleIdList = new List<int>();
         if (vehicleIdstrings != null)
         {
            foreach (string strId in vehicleIdstrings)
            {
               int vehicleId = Convert.ToInt32(strId);
               vehicleIdList.Add(vehicleId);
               if (bFlushCache)
               {
                  Vehicle vehicle = db.Vehicles.FirstOrDefault(x => x.Id == vehicleId);
                  if (vehicle != null)
                  {
                     if (vehicle.MaintenanceDataCacheFlush())
                        db.SaveChanges();
                  }
               }
            }
         }

         // Determine the count of all vehicles under consideration - all vehicles or just those for a given agency
         int agencyId = Convert.ToInt32(paramCollection["agencyId"]);
         int vehicleCount = agencyId == 0 ? db.Vehicles.Count() : db.Vehicles.Count(x => x.Owner.Id == agencyId);

         // Create the list of vehicles
         IOrderedQueryable<Vehicle> filteredVehicles;
         if (!string.IsNullOrEmpty(dtParams.sSearch))
         {
            if (agencyId == 0)
            {
               filteredVehicles = (IOrderedQueryable<Vehicle>) db.Vehicles
                  .Where(
                     a => a.Owner == null
                          &&
                          (a.ShortName.Contains(dtParams.sSearch) || a.ProductionNumber.Contains(dtParams.sSearch) ||
                           a.HEVModel.Contains(dtParams.sSearch)))
                  .OrderBy(a => a.ShortName)
                  .AsQueryable();
            }
            else
            {
               filteredVehicles = (IOrderedQueryable<Vehicle>) db.Vehicles
                  .Where(
                     a => a.Owner.Id == agencyId
                          &&
                          (a.ShortName.Contains(dtParams.sSearch) || a.ProductionNumber.Contains(dtParams.sSearch) ||
                           a.HEVModel.Contains(dtParams.sSearch)))
                  .OrderBy(a => a.ShortName)
                  .AsQueryable();
            }
         }
         else
         {
            if (agencyId == 0)
            {
               filteredVehicles = (IOrderedQueryable<Vehicle>) db.Vehicles
                  .Where(a => a.Owner == null)
                  .OrderBy(a => a.ShortName)
                  .AsQueryable();
            }
            else
            {
               filteredVehicles = (IOrderedQueryable<Vehicle>) db.Vehicles
                  .Where(a => a.Owner.Id == agencyId)
                  .OrderBy(a => a.ShortName)
                  .AsQueryable();
            }
         }

         int filteredCount = filteredVehicles.Count();

         // Sort the filetered list if needed.
         int sortCol_0 = Convert.ToInt32(paramCollection["iSortCol_0"]);

         if (sortCol_0 > 0)
         {
            // Loop across all sort parameters.  The first is processed as "OrderBy..." and
            // subsequent sorts are processed as "ThenBy..." giving a cumulative sort by
            // multiple columns/properties
            for (int i = 0; i < dtParams.iSortingCols; i++)
            {
               string sortDir = paramCollection[string.Format("sSortDir_{0}", i.ToString())];
               int sortCol = Convert.ToInt32(paramCollection[string.Format("iSortCol_{0}", i.ToString())]);

               switch (sortCol)
               {
                  case 1:
                     if (i == 0)
                     {
                        filteredVehicles = sortDir == "asc"
                           ? filteredVehicles.OrderBy(a => a.ShortName)
                           : filteredVehicles.OrderByDescending(a => a.ShortName);
                     }
                     else
                     {
                        filteredVehicles = sortDir == "asc"
                           ? filteredVehicles.ThenBy(a => a.ShortName)
                           : filteredVehicles.ThenByDescending(a => a.ShortName);
                     }

                     break;
                  case 2:
                     if (i == 0)
                     {
                        filteredVehicles = sortDir == "asc"
                           ? filteredVehicles.OrderBy(a => a.ProductionNumber)
                           : filteredVehicles.OrderByDescending(a => a.ProductionNumber);
                     }
                     else
                     {
                        filteredVehicles = sortDir == "asc"
                           ? filteredVehicles.ThenBy(a => a.ProductionNumber)
                           : filteredVehicles.ThenByDescending(a => a.ProductionNumber);
                     }
                     break;

                  case 3:
                     if (i == 0)
                     {
                        filteredVehicles = sortDir == "asc"
                           ? filteredVehicles.OrderBy(a => a.HEVModel)
                           : filteredVehicles.OrderByDescending(a => a.HEVModel);
                     }
                     else
                     {
                        filteredVehicles = sortDir == "asc"
                           ? filteredVehicles.ThenBy(a => a.HEVModel)
                           : filteredVehicles.ThenByDescending(a => a.HEVModel);
                     }
                     break;

                  case 4:
                     if (i == 0)
                     {
                        filteredVehicles = sortDir == "asc"
                           ? filteredVehicles.OrderBy(a => a.Active ? "Yes" : "No")
                           : filteredVehicles.OrderByDescending(a => a.Active ? "Yes" : "No");
                     }
                     else
                     {
                        filteredVehicles = sortDir == "asc"
                           ? filteredVehicles.ThenBy(a => a.Active ? "Yes" : "No")
                           : filteredVehicles.ThenByDescending(a => a.Active ? "Yes" : "No");
                     }
                     break;


                  case 5:
                     // Sort by last PM Data DateTime - We can't use vehicle's LastPmDataTime property because
                     // its a derived property value and not a property of the record in the database.  Instead
                     // we have to use direct references to Vehicle properties provided by LastPmDataTimeExpression().
                     if (i == 0)
                        filteredVehicles = sortDir == "asc"
                           ? filteredVehicles.OrderBy(Vehicle.LastPmDataTimeExpression())
                           : filteredVehicles.OrderByDescending(Vehicle.LastPmDataTimeExpression());
                     else
                        filteredVehicles = sortDir == "asc"
                           ? filteredVehicles.ThenBy(Vehicle.LastPmDataTimeExpression())
                           : filteredVehicles.ThenByDescending(Vehicle.LastPmDataTimeExpression());
                     break;
               }
            }
         }

         // Paging support for jQuery Datatables - Now that we have the filtered, sorted list of vehicles,
         // skip to the starting point in the list and take the number of vehicles needed to fill a page.
         List<Vehicle> dataVehicles = filteredVehicles
            .Skip(dtParams.iDisplayStart)
            .Take(dtParams.iDisplayLength)
            .ToList();

         // Create the output for DataTables
         //
         // We have the final list in final order.  Now transform that to create a list of string
         // arrays.  Each element in the list corresponds to a vehicle.  Each list element's array
         // holds the strings that correspond to the columns of the DataTable with any needed HTML embeded.
         // Although the model used in the CSHTML is a DTO we don't need it here because we are transforming
         // the data into the raw strings needed by DataTables.

         bool isAdmin = User.IsInRole(MobilEMSApp.HortonAdministratorRole) || User.IsInRole(MobilEMSApp.AgencyAdministratorRole);
         List<string[]> vehicleData = dataVehicles
            .Select(d => new[]
            {
               isAdmin
                  ? string.Format(@"
                     <a href='/Vehicle/Edit/{0}'>
                        <img src='/Images/edit_yellow_20x20.png' class='pad-bttm-2px' alt='Edit vehicle' title='Edit vehicle'>
                     </a>
                     <a href='/Vehicle/Delete/{0}'>
                        <img src='/Images/minus.png' class='pad-bttm-2px' alt='Delete vehicle' title='Delete vehicle' />
                     </a>
                     <img src='/Images/excel_20x20.png' class='pad-bttm-2px' alt='Export maintenance data to Excel' onclick='doPopUp({0})' />
                     ", d.Id)
                   : string.Format(@"
                     <img src='/Images/excel_20x20.png' class='pad-bttm-2px' alt='Export maintenance data to Excel' onclick='doPopUp({0})' />
                     ", d.Id),
               "<a href='/Vehicle/Details/" + d.Id + "'>" + d.ShortName + "</a>",
               d.ProductionNumber,
               d.HEVModel,
               d.Active ? "Yes" : "No",
               !d.LastPmDataTime.HasValue
                  ? "--"
                  : string.Format("<a href='/PMReport/Details/{0}'><img src='/images/report_20x20.png'/> {1} {2}</a>", d.Id,
                     d.LastPmDataTime.Value.ToBrowserTime().ToShortDateString(),
                     d.LastPmDataTime.Value.ToBrowserTime().ToShortTimeString()),
               String.IsNullOrEmpty(d.PmUpdateStatus)
                  ? String.Empty
                  : string.Format("<input type='checkbox' {0} />",
                     (vehicleIdList.Contains(d.Id) ? "checked='checked' " : String.Empty)),
               d.PmUpdateStatus,
               d.Id.ToString(CultureInfo.InvariantCulture)
            }).ToList();

         return new DataTableResults
         {
            sEcho = dtParams.sEcho,
            iTotalRecords = vehicleCount,
            iTotalDisplayRecords = filteredCount,
            aaData = vehicleData
         };
      }
   }
}
