using System;
using System.Collections.Specialized;
using System.Configuration;
using System.EnterpriseServices;
using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Net.Mail;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceModel.Web;
using System.Web.Mvc;
using System.Web.Security;
using CheckFirst.Data;
using CheckFirst.Data.Utility;
using CheckOhioFirst.Web.Models;
using CheckOhioFirst.Web.Models.Event;
using CheckOhioFirst.Web.Utility;
using Glimpse.Core.ClientScript;
using Halcyon.Web;
using Microsoft.Ajax.Utilities;
using Microsoft.ReportingServices.Interfaces;
using Postal;
using WebMatrix.WebData;

namespace CheckOhioFirst.Web.Controllers
{
   #region Helper Classes
   //public class MyErrorAttribute : FilterAttribute, IExceptionFilter
   //{
   //    public virtual void OnException(ExceptionContext filterContext)
   //    {
   //        if (filterContext == null)
   //        {
   //            throw new ArgumentNullException("filterContext");
   //        }
   //        if (filterContext.Exception != null)
   //        {
   //            filterContext.ExceptionHandled = true;
   //            filterContext.HttpContext.Response.Clear();
   //            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
   //            filterContext.HttpContext.Response.StatusCode = (int)System.Net.HttpStatusCode.InternalServerError;
   //            filterContext.Result = new JsonResult() { Data = filterContext.Exception.Message };
   //        }
   //    }
   //}

   //public class HandleJsonExceptionAttribute : ActionFilterAttribute
   //{
   //    public override void OnActionExecuted(ActionExecutedContext filterContext)
   //    {
   //        if (filterContext.HttpContext.Request.IsAjaxRequest() && filterContext.Exception != null)
   //        {
   //            filterContext.HttpContext.Response.StatusCode = (int)System.Net.HttpStatusCode.InternalServerError;
   //            filterContext.Result = new JsonResult()
   //            {
   //                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
   //                Data = new
   //                {
   //                    filterContext.Exception.Message,
   //                    filterContext.Exception.StackTrace
   //                }
   //            };
   //            filterContext.ExceptionHandled = true;
   //        }
   //    }
   //}

   public class EventSmtpClient
   {
      public SmtpClient SmtpClient
      {
         get { return _smtpClient; }
      }
      private readonly SmtpClient _smtpClient = new SmtpClient { Host = "localhost", Port = 25 };

      virtual public void Send(MailMessage msg)
      {
         _smtpClient.Send(msg);
      }
   }

   public class EventSecurity  // A wrapper around WebSecurity allowing mock implementations in unit tests.
   {
      virtual public string CurrentUserName
      {
         get
         {
            if (WebSecurity.Initialized && WebSecurity.IsAuthenticated)
               return WebSecurity.CurrentUserName;

            return null;
         }
      }

      virtual public bool Initialized
      {
         get { return WebSecurity.Initialized; }
      }

      virtual public bool IsAuthenticated
      {
         get { return WebSecurity.IsAuthenticated; }
      }
   }

   public class EventPaymentProcessor
   {
      public bool ProcessPayment(Invoice invoice, Invoice priorInvoice, CreditCardDTO creditCard)
      {
         invoice.PaymentProcessor = "*** No Processor ***";
         invoice.ExternalPaymentID = "*** No Payment ***";
         var amtProcessed = invoice.TotalAmount;
         if (priorInvoice != null)
         {
            amtProcessed -= priorInvoice.TotalAmount;
            invoice.ExternalPaymentID += String.Format("Credit ${0:N} from Invoice {1}; amt. processed: ${2:N}",
                priorInvoice.TotalAmount, priorInvoice.Id, amtProcessed);
         }
         return true;
      }
   }

   #endregion

   public class EventController : Controller
   {
      #region Class Variables

      private const string InvoiceResultSessionId = "Invoice";
      private const string EventRegistrationDtoSessionId = "ErDto";

      private readonly string _priorCreditLineItemTitle = ConfigurationManager.AppSettings["CreditForPriorInvoiceName"] ??
                                            "Credit For Prior Purchase(s)";
      private readonly CheckFirstModelContainer _db = new CheckFirstModelContainer();

      public EventSecurity Security
      {
         get { return _security; }
         set { _security = value; }
      }
      private EventSecurity _security = new EventSecurity();

      public EventSmtpClient SmtpClient
      {
         get { return _smtpClient; }
         set { _smtpClient = value; }
      }
      private EventSmtpClient _smtpClient = new EventSmtpClient();

      public EventPaymentProcessor PaymentProcessor
      {
         get { return _paymentProcessor; }
         set { _paymentProcessor = value; }
      }
      private EventPaymentProcessor _paymentProcessor = new EventPaymentProcessor();

      #endregion

      #region Constructor & Dispose
      public EventController()
      {
         ViewBag.CategoryGlyph = "/Images/Symbols/150/Solid/Calendar.png";
      }

      protected override void Dispose(bool disposing)
      {
         _db.Dispose();
         base.Dispose(disposing);
      }
      #endregion

      #region Utility Methods

      private void SaveToSession(string sessionId, object obj)
      {
         Session.Remove(sessionId);
         Session[sessionId] = obj;
      }

      private static void AddPriorInvoiceReferences(Invoice priorInvoice, InvoiceResult invoiceResult)
      {
         if (priorInvoice != null)
         {
            invoiceResult.AddDataPair(InvoiceResult.TransactionPriorInvoiceId,
                priorInvoice.Id.ToString(CultureInfo.InvariantCulture));
            invoiceResult.AddDataPair(InvoiceResult.TransactionPriorInvoiceAmtId,
                priorInvoice.TotalAmount.ToString("F"));
         }
      }

      private Invoice RegistrationToInvoice
          (Person person, double priorAmt, EventRegistrationDTO eventRegistrationDto)
      {
         EventInfo eventInfo = GetEvent(eventRegistrationDto.Event.Id);
         // We can't bring the event description up from the DTO because we allow
         // HTML formatted content in the description and that could be used
         // as an exploit against the site.  So we put it back into the DTO
         // from the DB copy of the Event information.
         eventRegistrationDto.Event.Description = eventInfo.Description;
         Mapper.Map(eventInfo.Roles, eventRegistrationDto.Event.Roles);

         Invoice invoice = new Invoice
         {
            Id = 0,
            Date = DateTime.UtcNow,
            TotalAmount = 0.0d,
            Checksum = "",
            Store = eventInfo,
            Purchaser = person,
            BuyerRole = GetBuyerRole(eventRegistrationDto.RoleId)
         };

         foreach (EventLineItemDTO regItem in eventRegistrationDto.Event.LineItems)
         {
            var inventory = _db.Inventories.SingleOrDefault(i => i.Id == regItem.Inventory.Id);
            if (inventory == null)
               throw new Exception("Requested item [" + regItem.Inventory.Name + "] not found");

            if (regItem.Inventory.Name == _priorCreditLineItemTitle) continue;
            if (regItem.Quantity > 0)
            {
               inventory.QuantityRemaining -= regItem.Quantity;

               LineItem lineItem = new LineItem(inventory, regItem.Quantity, regItem.CostEach);

               if (regItem.PersonalizationsIds != null)
               {
                  foreach (int pId in regItem.PersonalizationsIds)
                  {
                     Person liPerson = _db.People.FirstOrDefault(p => p.Id == pId);
                     lineItem.Personalizations.Add(liPerson);
                  }
               }
               invoice.LineItems.Add(lineItem);
               invoice.TotalAmount += lineItem.Quantity * lineItem.CostEach;
            }
         }

         Inventory priorCreditItem =
             invoice.LineItems
                 .Select(li => li.Inventory)
                 .FirstOrDefault(i => i.Name == _priorCreditLineItemTitle) ?? _db.Inventories.FirstOrDefault(
                     i => i.Store.Id == eventRegistrationDto.Event.Id && i.Name == _priorCreditLineItemTitle);

         LineItem creditLineItem = new LineItem(priorCreditItem, 1, priorAmt);
         invoice.LineItems.Add(creditLineItem);

         return invoice;
      }

      private BuyerRole GetBuyerRole(string idStr)
      {
         BuyerRole role = null; // Load Role object, if user picked a role
         int roleId;
         if (int.TryParse(idStr, out roleId))
         {
            if (roleId != 0)
            {
               role = _db.BuyerRoles.SingleOrDefault(r => r.Id == roleId);
               if (role == null)
                  throw new Exception("Database error - role Id [" + idStr + "] not found");
            }
         }
         return role;
      }

      private EventInfo GetEvent(int id)
      {
         if (id == 0)
            return null;

         return _db.Stores.OfType<EventInfo>() // Load Event object
             .Include("SponsoringCompany")
             .Include("Schedule")
             .Include("Roles")
             .Include("Inventory")
             .SingleOrDefault(e => e.Id == id);
      }

      private Invoice GetMostRecentInvoice(int userId, int eventId)
      {
         // Get list of invoices for this user and event
         List<Invoice> invoices = _db.Invoices
             .Include("Store")
             .Include("LineItems")
             .Include("LineItems.Inventory")
             .Include("LineItems.Personalizations")
             .Include("BuyerRole")
             .Where(a => a.Purchaser.Id == userId && a.Store.Id == eventId)
             .ToList();
         if (invoices.Count == 0)
            return null;

         return invoices.FirstOrDefault(i => i.Date == invoices.Max(j => j.Date));
      }

      private void BackOutInvoice(Invoice invoice)
      {
         if ((invoice != null) && (invoice.LineItems != null) && (invoice.LineItems.Count > 0))
            foreach (var li in invoice.LineItems)
               li.Inventory.QuantityRemaining += li.Quantity;
      }

      private Person GetPerson(int userId = 0)
      {
         if (userId != 0)
            return _db.People.Include("Employer").FirstOrDefault(p => p.Id == userId);

         return _db.People.Include("Employer").FirstOrDefault(p => p.Email == Security.CurrentUserName);
      }

      private void SetCompanyList(string selectedCompanyId)
      {
         /*
          * Put list of company names in viewbag - used to construct HTML select control
          * allowing user to select a sponsoring company.
          */
         var selectList =
             new SelectList(_db.Companies.ToList().OrderBy(c => c.Name),
                 "Name", "Name", selectedCompanyId);
         if (!String.IsNullOrEmpty(selectedCompanyId))
         {
            SelectListItem item = selectList.FirstOrDefault(s => s.Value == selectedCompanyId);
            if (item != null)
               item.Selected = true;
            // selectList.Single(s => s.Value == selectedCompanyId).Selected = true;
         }
         ViewBag.Sponsor = selectList;
         ViewBag.EditAllowed = true;
         ViewBag.SelectedSponsorId = selectedCompanyId;
      }

      public void DeleteObjectsInList<T>(List<T> list)
      {
         if (list != null && list.Count > 0)
         {
            foreach (T li in list)
            {
               _db.DeleteObject(li);
            }
         }
      }

      private List<DisplayDocumentDTO> GetEventDocs(int eventId)
      {
         List<Document> documents = _db.DocumentSet
             .Where(d => d.Event.Id == eventId)
             .Where(d => d.DeletedOn == null)
             .ToList();

         return Mapper.Map<List<Document>, List<DisplayDocumentDTO>>(documents);
      }

      /// <summary>
      /// Gets an Invoice object correctly loaded for Purchase methods.
      /// </summary>
      /// <param name="id">Id of the invoice</param>
      /// <returns>Invoice object</returns>
      private Invoice GetInvoice(int id = 0)
      {
         if (id == 0)
            return null;

         return _db.Invoices
             .Include("Store")
             .Include("Purchaser.Employer")
             .Include("LineItems")
             .Include("LineItems.Inventory")
             .Include("LineItems.Personalizations")
             .Include("BuyerRole")
             .FirstOrDefault(x => x.Id == id);
      }

      private SelectList GetContacts(Person person)
      {
         // Assume an empty list of employees
         List<SelectListItem> listEmployees = new List<SelectListItem>();

         // Get content for the employee list if we can
         if (person.Employer == null)
            _db.LoadProperty(person, "Employer");

         if (person.Employer != null)
         {
            Company company = _db.Companies.Include("Employees").FirstOrDefault(c => c.Id == person.Employer.Id);
            if ((company != null) && (company.Employees != null) && (company.Employees.Count > 0))
            {
               listEmployees = company.Employees
                   .Select(c => new SelectListItem { Value = Convert.ToString(c.Id), Text = c.FullName })
                   .ToList();
            }
         }

         // Return a SelectList of the list of employees regardless if it is empty or not
         return new SelectList(listEmployees, "Value", "Text");
      }

      #endregion

      #region Event Read Methods

      public ActionResult Index()
      {
         List<EventInfo> eventList = _db.Stores.OfType<EventInfo>()
             .Include("Schedule")
             .Where(e => e.Schedule.Select(s => s.End).Max() >= DateTime.Now)
             .OrderBy(e => e.Schedule.Select(s => s.Start).Min())
             .ToList();
         return View(eventList);
      }

      [Authorize(Roles = "Administrator")]
      public ActionResult Admin()
      {
         List<EventInfo> eventList = _db.Stores.OfType<EventInfo>()
             .Include("Schedule")
             .Include("Roles")
             .Include("Inventory")
             .Include("Invoices")
             .ToList();
         return View(eventList);
      }

      public ActionResult Details(int id = 0)
      {
         var eventInfo = _db.Stores.OfType<EventInfo>()
             .Include("Schedule")
             .Include("Roles")
             .Include("Inventory")
             .Include("SponsoringCompany")
             .SingleOrDefault(e => e.Id == id);
         if (eventInfo == null)
         {
            return HttpNotFound("Event record not found");
         }
         var eventInfoDto = new EventInfoDTO(eventInfo);

         var documents = _db.DocumentSet
             .Where(d => d.Event.Id == id & d.Active)
             .OrderBy(d => d.Category)
             .ThenBy(d => d.Name)
             .ToList();

         if (documents.Count > 0)
         {
            eventInfoDto.ResourcesLinks.Documents = new List<DisplayDocumentDTO>();
            Mapper.Map(documents, eventInfoDto.ResourcesLinks.Documents);
            eventInfoDto.ResourcesLinks.DocCategories = documents
            .GroupBy(x => x.Category)
            .Select(g => g.OrderBy(x => x.Category).First())
            .Select(x => x.Category)
            .ToArray();

         }
         //var eventInfoDto = Mapper.Map<EventInfo, EventInfoDTO>(eventInfo);
         //eventInfoDto.SetFlags();
         return View(eventInfoDto);
      }

      private EventInfo DetailsEventInfo(int id = 0)
      {
         return _db.Stores.OfType<EventInfo>()
              .Include("Schedule")
              .Include("Roles")
              .Include("Inventory")
              .Include("SponsoringCompany")
              .SingleOrDefault(e => e.Id == id);
      }

      public ActionResult ItemsForStore(int sId)
      {
         var eventInfo = _db.Stores.OfType<EventInfo>()
             .Include("Schedule")
             .Include("Roles")
             .Include("Inventory")
             .Include("Inventory.AllowedRoles")
             .Include("SponsoringCompany")
             .SingleOrDefault(e => e.Id == sId);
         if (eventInfo == null)
         {
            return HttpNotFound();
         }

         EventInfoDTO eventInfoDto = new EventInfoDTO(eventInfo);

         EventInventoriesDTO curInventory = new EventInventoriesDTO { StoreId = sId, Inventories = eventInfoDto.Inventories };

         return PartialView("_InventoryTab", curInventory);
      }

      #endregion

      #region Event Create Methods

      [Authorize(Roles = "Administrator")]
      public ActionResult Create()
      {
         SetCompanyList("");
         return View();
      }

      [HttpPost]
      [ValidateAntiForgeryToken]
      [Authorize(Roles = "Administrator")]
      public ActionResult Create(EventInfoDTO eventInfoDto, int saveAction = 0)
      {
         try
         {
            if (ModelState.IsValid)
            {
               var eventInfo = eventInfoDto.MapDTOToEvent(_db);

               if (eventInfoDto.IsMatchmakingEvent)
               {
                  BuyerRole matchRole = BuyerRole.CreateBuyerRole(0, "Supplier");
                  matchRole.Predicate = "ProfileRequired";
                  eventInfo.Roles.Add(matchRole);

                  matchRole = BuyerRole.CreateBuyerRole(0, "Buyer");
                  matchRole.Predicate = "ProfileRequired";
                  eventInfo.Roles.Add(matchRole);
               }

               Inventory priorPayment = Inventory.CreateInventory(0, "Prior Activity",
                   _priorCreditLineItemTitle, "Total amount of the prior invoice (if any).");
               priorPayment.Cost = 0;
               eventInfo.Inventory.Add(priorPayment);

               eventInfo.SponsoringCompany = string.IsNullOrEmpty(eventInfo.Sponsor)
                                                 ? null
                                                 : _db.Companies.FirstOrDefault(c => c.Name == eventInfo.Sponsor);
               _db.Stores.AddObject(eventInfo);
               _db.SaveChanges();

               eventInfoDto.Id = eventInfo.Id;     // Transfer ID back for unit tests

               if (saveAction == 1)
                  return RedirectToAction("Edit", new { eventInfoDto.Id });

               return RedirectToAction("Admin");
            }

            return View(eventInfoDto);
         }
         catch (DataException e)
         {
            ModelState.AddModelError("", "Unable to save changes.  Try again, and if the problem persists, contact our help desk.\n"
                + e.Message);

            SetCompanyList(string.IsNullOrEmpty(eventInfoDto.Sponsor) ? "" : eventInfoDto.Sponsor.Trim());
            return View(eventInfoDto);
         }
      }
      #endregion

      #region Event Update/Edit Methods

      [Authorize(Roles = "Administrator")]
      public ActionResult Edit(int id = 0)
      {
         var eventInfo = _db.Stores.OfType<EventInfo>()
             .Include("Schedule")
             .Include("Roles")
             .Include("Inventory")
             .Include("Inventory.AllowedRoles")
             .Include("SponsoringCompany")
             .SingleOrDefault(e => e.Id == id);
         if (eventInfo == null)
         {
            return HttpNotFound();
         }

         var eventInfoDto = new EventInfoDTO(eventInfo);

         List<Document> documents = _db.DocumentSet
             .Where(d => d.Event.Id == id)
             .Where(d => d.DeletedOn == null)
             .ToList();

         eventInfoDto.ResourcesLinks.Documents = Mapper.Map<List<Document>, List<DisplayDocumentDTO>>(documents);
         //Creator.Context = db;
         //var eventInfoDto = Mapper.Map<EventInfo, EventInfoDTO>(eventInfo);
         //eventInfoDto.SetFlags();
         SetCompanyList(string.IsNullOrEmpty(eventInfo.Sponsor) ? "" : eventInfo.Sponsor.Trim());
         return View(eventInfoDto);
      }

      [HttpPost]
      [ValidateAntiForgeryToken]
      [Authorize(Roles = "Administrator")]
      public ActionResult Edit(EventInfoDTO eventInfoDto, int saveAction = 0)
      {
         try
         {
            if (ModelState.IsValid)
            {

               if (_db == null)
                  throw new Exception("Internal reference to database is null");

               var eventInfo = _db.Stores.OfType<EventInfo>()
                   .Include("Schedule")
                   .Include("Roles")
                   .Include("Inventory")
                   .Include("Inventory.AllowedRoles")
                   .Include("Roles.AllowedInventory")
                   .Include("SponsoringCompany")
                   .SingleOrDefault(e => e.Id == eventInfoDto.Id);
               if (eventInfo == null)
               {
                  return HttpNotFound();
               }
               /*
                * Event Dates - Explicitly delete any existing EventDay object not matched by Id in the DTO
                */
               var existingEventDays = eventInfo.Schedule.ToList();
               foreach (EventDay existingEventDay in existingEventDays)
               {
                  var foundEventDay = eventInfoDto.Schedule.SingleOrDefault(ed => ed.Id == existingEventDay.Id);
                  if (foundEventDay == null)
                     _db.DeleteObject(existingEventDay);
               }
               /*
                * Event Roles - Explicitly delete any existing BuyerRole object not matched by Id or name in the DTO
                */
               List<BuyerRole> existingRoles = eventInfo.Roles.ToList();
               foreach (BuyerRole existingRole in existingRoles)
               {
                  if (eventInfoDto.Roles.SingleOrDefault(fr => fr.Id == existingRole.Id) == null) // Not found by Id
                  {
                     var dtoRole = eventInfoDto.Roles.SingleOrDefault(dr => dr.Name == existingRole.Name);
                     if (dtoRole != null) // Matched by name - set the ID in the DTO Role
                     {
                        dtoRole.Id = existingRole.Id;
                     }
                     else // Existing role not found in DTO Roles - delete it
                     {
                        _db.DeleteObject(existingRole);
                     }
                  }
               }

               eventInfo = eventInfoDto.MapDTOToEvent(_db, eventInfo);
               eventInfo.SponsoringCompany = string.IsNullOrEmpty(eventInfo.Sponsor)
                     ? null
                     : _db.Companies.FirstOrDefault(c => c.Name == eventInfo.Sponsor);

               _db.SaveChanges();

               switch (saveAction)
               {
                  case 0:
                     return RedirectToAction("Admin");
                  case 2:
                     return RedirectToAction("Create", "Document", new { eId = eventInfo.Id });
               }

               //if (saveAction != 1)
               //    return RedirectToAction("Admin");

               SetCompanyList(string.IsNullOrEmpty(eventInfoDto.Sponsor) ? "" : eventInfoDto.Sponsor.Trim());
               eventInfoDto.ResourcesLinks.Documents = GetEventDocs(eventInfoDto.Id);

               return View(eventInfoDto);
            }

            SetCompanyList(string.IsNullOrEmpty(eventInfoDto.Sponsor) ? "" : eventInfoDto.Sponsor.Trim());
            eventInfoDto.ResourcesLinks.Documents = GetEventDocs(eventInfoDto.Id);

            return View(eventInfoDto);
         }
         catch (Exception e)
         {
            ModelState.AddModelError(String.Empty, "Unable to save changes.  Try again, and if the problem persists, contact our help desk.");
            ModelState.AddModelError(String.Empty, e.Message);

            SetCompanyList(string.IsNullOrEmpty(eventInfoDto.Sponsor) ? "" : eventInfoDto.Sponsor.Trim());
            eventInfoDto.ResourcesLinks.Documents = GetEventDocs(eventInfoDto.Id);

            return View(eventInfoDto);
         }
      }

      #endregion

      #region Event Delete Methods

      [Authorize(Roles = "Administrator")]
      public ActionResult Delete(int id = 0)
      {
         return Details(id);
      }

      [HttpPost, ActionName("Delete")]
      [ValidateAntiForgeryToken]
      [Authorize(Roles = "Administrator")]
      public ActionResult DeleteConfirmed(int id)
      {
         EventInfo eventInfo = _db.Stores
             .OfType<EventInfo>()
             .Include("Inventory")
             .Include("Inventory.AllowedRoles")
             .Include("Inventory.ItemsSold")
             .Include("Roles")
             .Include("Roles.AllowedInventory")
            //.Include("Attendees")
             .Include("Invoices")
             .Include("Schedule")
             .Single(e => e.Id == id);

         var inventoryList = eventInfo.Inventory.ToList();
         foreach (Inventory inventory in inventoryList)
         {
            DeleteObjectsInList<LineItem>(inventory.ItemsSold.ToList());
            _db.DeleteObject(inventory);
         }

         DeleteObjectsInList<BuyerRole>(eventInfo.Roles.ToList());
         DeleteObjectsInList<EventDay>(eventInfo.Schedule.ToList());
         //DeleteObjectsInList<EventAttendee>(eventInfo.Attendees.ToList());
         DeleteObjectsInList<Invoice>(eventInfo.Invoices.ToList());
         _db.Stores.DeleteObject(eventInfo);
         _db.SaveChanges();
         return RedirectToAction("Admin");
      }
      #endregion

      #region Registration Methods

      [Authorize]
      public ActionResult EventRegistrationSelect(int id, int userId = 0, int invoiceId = 0)
      {
         Session.Remove(EventRegistrationDtoSessionId);

         int userIdActual = userId == 0 ? GetPerson().Id : userId;
         Invoice invoice = null;

         List<Invoice> invoices = _db.Invoices
             .Include("Purchaser")
             .Where(a => a.Purchaser.Id == userIdActual && a.Store.Id == id)
             .ToList();

         if (invoiceId != 0)
         {
            invoice = invoices.FirstOrDefault(i => i.Id == invoiceId);
         }
         else if (invoices.Count > 0)
         {
            invoice = invoices.FirstOrDefault(i => i.Date == invoices.Max(j => j.Date));
         }

         if (invoice != null)
            return RedirectToAction("Confirmation", new { invoice.Id });

         return RedirectToAction("EventRegistration", new { id });
      }

      [Authorize]
      public ActionResult EventRegistration(int id, int uId = 0, int rId = 0)
      {
         // If the DTO is already in the session and the event Id is the same we can just use it as is.
         EventRegistrationDTO eventRegistrationDto = (EventRegistrationDTO)Session[EventRegistrationDtoSessionId];
         if ((eventRegistrationDto == null) || (eventRegistrationDto.Event.Id != id))
         {
            Session.Remove(EventRegistrationDtoSessionId);
            EventInfo eventInfo = GetEvent(id);
            if (eventInfo == null)
               throw new ArgumentException("Event record not found");

            Person person = (uId != 0 && Roles.IsUserInRole("Administrator")) ? GetPerson(uId) : GetPerson();
            eventRegistrationDto = EventRegistrationDTO.MapEventToDTO(eventInfo, person);
            EventInvoiceDTO invoiceDto = new EventInvoiceDTO { Id = 0 };

            if ((person != null) && (person.Employer != null))
            {
               invoiceDto.PurchaserEmployerId = person.Employer.Id;
               Invoice priorInvoice = GetMostRecentInvoice(person.Id, id);

               if (priorInvoice != null)
               {
                  // only set flag about prior invoice is prior invoice has any quantity on line items (except prior credit item)
                  // this enables the re-selection of the role
                  // this condition should only occur if an admin has processed a full refund for the prior invoice
                  if (priorInvoice.LineItems.FirstOrDefault(li => li.Inventory.Name != _priorCreditLineItemTitle && li.Quantity > 0) != null)
                     eventRegistrationDto.HasPriorInvoice = true;

                  //get the role id for last invoice.
                  eventRegistrationDto.IsAdmin = Roles.IsUserInRole("Administrator") &&
                                                 person.Id != GetPerson().Id;
                  if (priorInvoice.BuyerRole != null)
                  {
                     eventRegistrationDto.RoleId =
                         priorInvoice.BuyerRole.Id.ToString(CultureInfo.InvariantCulture);
                     eventRegistrationDto.BuyerRole = Mapper.Map<BuyerRole, BuyerRoleDTO>(priorInvoice.BuyerRole);
                  }
               }
               else if (rId != 0)
               {
                  eventRegistrationDto.RoleId = rId.ToString(CultureInfo.InvariantCulture);
               }
            }
         }
         return View(eventRegistrationDto);
      }

      [HttpPost]
      [ValidateAntiForgeryToken]
      [Authorize]
      public ActionResult EventRegistration(EventRegistrationDTO eventRegistrationDto)
      {
         try
         {
            // Re-populate eventRegistrationDto with event details in case we have to redisplay it
            EventInfo eventInfo = GetEvent(eventRegistrationDto.Event.Id);
            if (eventInfo == null)
               throw new ArgumentException("Event record not found");

            Mapper.Map(eventInfo, eventRegistrationDto.Event);

            // Build the invoice
            Person person = GetPerson(eventRegistrationDto.User.Id);                           // Load Person object
            if (person == null)
               throw new Exception("Database error - logged in user not found");

            double priorAmt = 0.0d;
            Invoice priorInvoice = GetMostRecentInvoice(eventRegistrationDto.User.Id, eventRegistrationDto.Event.Id);
            if (priorInvoice != null)
            {
               priorAmt = priorInvoice.TotalAmount * (-1);

               // Backout the prior invoice.  We do this before we create the new invoice
               // so the inventory totals are recalculated before the new invoice takes
               // away from them.  The changes won't be saved unless we get through all
               // the data checks below and we also save the new invoice.
               BackOutInvoice(priorInvoice);
            }
            /*
            * Create a new invoice for this registration / purchase
            */

            Invoice invoice = RegistrationToInvoice(person, priorAmt, eventRegistrationDto);

            if ((priorInvoice != null) && (invoice.IsSameAs(priorInvoice, _priorCreditLineItemTitle)))
               throw new Exception("Registration with same selections already exists");

            SaveToSession(EventRegistrationDtoSessionId, eventRegistrationDto);
            return RedirectToAction("Purchase", new { id = 0 });
         }
         catch (Exception exception)
         {
            ModelState.AddModelError(string.Empty,
                "Unable to complete registration request.  Try again, and if the problem persists, contact our help desk.");
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(eventRegistrationDto);
         }
      }

      [Authorize]
      public PartialViewResult EventRegistrationLineItems(RegisterLineItemsDataDTO lineData)
      {
         DateTime currentDate = DateTime.UtcNow;
         List<Inventory> inventoryList = null;

         EventInfo eventInfo = GetEvent(lineData.eventId);

         BuyerRole buyerRole = (lineData.regType == 0) ? null : _db.BuyerRoles.SingleOrDefault(r => r.Id == lineData.regType);
         if (buyerRole == null)
         {
            inventoryList = _db.Inventories
                .Where(i => i.Store.Id == lineData.eventId && i.AllowedRoles.Count == 0)
                .OrderBy(i => i.Category)
                .ThenBy(i => i.Name)
                .ToList();
         }
         else
         {
            buyerRole.AllowedInventory.Load();
            inventoryList = _db.Inventories
                .Where(i => i.Store.Id == lineData.eventId && i.AllowedRoles.Count == 0).ToList();
            inventoryList.AddRange(buyerRole.AllowedInventory);
         }

         inventoryList = inventoryList.Where(i => (i.FirstDateAvailable == null || i.LastDateAvailable == null) || (i.FirstDateAvailable <= currentDate)).OrderBy(i => i.Category).ThenBy(i => i.Name).ToList();

         /*
          * Load Attendee (Person) object - there should be an attendee object if the
          * user has registered for the event before this
          */
         Person person = (lineData.userId != 0 && Roles.IsUserInRole("Administrator")) ? GetPerson(lineData.userId) : GetPerson();
         if (person.Employer != null)
         {
            person.Employer.BuyerProfileReference.Load();
            person.Employer.SellerProfileReference.Load();
         }

         List<Invoice> invoices = _db.Invoices
             .Include("Purchaser")
             .Include("LineItems")
             .Where(i => i.Purchaser.Id == person.Id && i.Store.Id == lineData.eventId)
             .ToList();

         if (lineData.regType != 0)
            ViewBag.ContactList = GetContacts(person);

         /*
          * Find the most recent invoice for this user and pull the line items from that invoice.
          */
         List<LineItem> priorLineItems = null;
         EventInvoiceDTO invoiceDto = new EventInvoiceDTO { Id = 0 };

         invoiceDto.IsAdmin = Roles.IsUserInRole("Administrator");
         invoiceDto.IsMatchmaker = eventInfo.IsMatchmakingEvent;

         if (person.Employer != null)
            invoiceDto.PurchaserEmployerId = person.Employer.Id;

         if (invoices.Any())
         {
            Invoice priorInvoice = invoices.FirstOrDefault(i => i.Date == invoices.Max(j => j.Date));

            // Can set HasPriorInvoice here to allow new role selection if all line items in priorInvoice are quantity 0
            invoiceDto.HasPriorInvoice = true;

            invoiceDto.Id = priorInvoice.Id;
            invoiceDto.Date = priorInvoice.Date;
            invoiceDto.TotalAmount = priorInvoice.TotalAmount;

            if (priorInvoice.LineItems.Any())
            {
               foreach (var li in priorInvoice.LineItems)
               {
                  li.InventoryReference.Load();
                  li.Personalizations.Load();
               }
               priorLineItems = priorInvoice.LineItems.ToList();
            }
         }

         /*
          * Convert all allowed inventory to line items to be displayed.  Match any items in the
          * inventory with line items from the invoice and set the quantity to the value
          * from the last invoice.
          */
         var eiDtoList = Mapper.Map<List<Inventory>, List<EventInventoryDTO>>(inventoryList);

         foreach (EventInventoryDTO eiDto in eiDtoList)
         {
            if (eiDto.Name != _priorCreditLineItemTitle)
            {
               EventLineItemDTO eliDto = new EventLineItemDTO();
               LineItem priorLineItem = (priorLineItems == null)
                   ? null
                   : priorLineItems.FirstOrDefault(li => li.Inventory.Id == eiDto.Id);

               eliDto.Id = 0;
               eliDto.Inventory = eiDto;
               eliDto.CostEach = eiDto.Cost;
               eliDto.Quantity = (priorLineItem == null) ? 0 : priorLineItem.Quantity;

               if (priorLineItem != null)
               {
                  int[] list = priorLineItem.Personalizations.Select(p => p.Id).ToArray();
                  eliDto.PersonalizationsIds = list;
               }

               invoiceDto.LineItems.Add(eliDto);
            }
         }

         //get the user company profile role
         string companyRole = null;
         if (person.Employer != null)
         {
            if (person.Employer.BuyerProfile != null && person.Employer.SellerProfile != null)
               companyRole = "Buyer and Supplier";
            else if (person.Employer.BuyerProfile != null)
               companyRole = "Buyer";
            else if (person.Employer.SellerProfile != null)
               companyRole = "Supplier";

            invoiceDto.CompanyExists = true;
         }

         invoiceDto.CompanyRole = companyRole;

         //Get the buyer Role Information.
         if (buyerRole != null)
         {
            invoiceDto.IsProfileRequired = buyerRole.Predicate.Contains("ProfileRequired");
            invoiceDto.BuyerRole = buyerRole.Name;
         }

         return PartialView("EventRegistrationLineItems", invoiceDto);
      }

      #endregion

      #region Payment/Purchase Methods

      /// <summary>
      /// Display the inner purchase form (displayed in iframe of parent form/window)
      /// </summary>
      /// <param name="id">int Id of invoice to be processed for purchase</param>
      /// <returns>View of page</returns>
      [HttpGet]
      [Authorize]
      [RequireHttps]
      public ActionResult PurchaseForm(int id)
      {
         PurchaseDTO purchaseDto = null;
         Person person = null;
         try
         {
            // Get Registration DTO from session
            EventRegistrationDTO eventRegistrationDto = (EventRegistrationDTO)Session[EventRegistrationDtoSessionId];

            // Build the invoice
            person = GetPerson(eventRegistrationDto.User.Id);
            if (person == null)
               throw new Exception("User reference not found");

            Invoice priorInvoice = GetMostRecentInvoice(person.Id, eventRegistrationDto.Event.Id);
            double priorAmt = priorInvoice == null ? 0.0d : priorInvoice.TotalAmount * (-1);
            Invoice invoice = RegistrationToInvoice(person, priorAmt, eventRegistrationDto);

            // Build the DTO
            purchaseDto = new PurchaseDTO(invoice);
            if (purchaseDto.EventInfoDto == null)
               throw new Exception("Event reference not found");

            // Get the invoice Result and log we are presenting the purchase form
            InvoiceResult invoiceResult = invoice.Result;
            invoiceResult.AddLogEntry(Security.CurrentUserName, "Payment Form Presented");
            SaveToSession(InvoiceResultSessionId, invoiceResult);
         }
         // All exceptions trapped setting up the PurchaseForm are logged and E-mail is sent to administrator
         catch (Exception ex)
         {
            ModelState.AddModelError(String.Empty, ex.Message);
            string user = person != null ? person.FullName + "(" + person.Email + ")" : "Unknown";
            string eventDescription = "Unknown";
            if (purchaseDto != null)
               eventDescription = purchaseDto.eventName ?? "Unknown";
            SystemFailureEmail.Send("Purchase Form Setup", ex, user, eventDescription);
         }
         return View(purchaseDto);
      }

      /// <summary>
      /// Display the outer (parent page with iframe) Purchase form given an invoice Id
      /// </summary>
      /// <param name="id">Id of the invoice to be paid.</param>
      /// <returns>View of page</returns>
      [HttpGet, ActionName("Purchase")]
      [Authorize]
      [RequireHttps]
      public ActionResult Purchase(int id)
      {
         PurchaseDTO purchaseDto = null;
         Person person = null;
         InvoiceResult invoiceResult = null;
         try
         {
            Invoice invoice = GetInvoice(id);

            // If the invoice has never been saved...
            if ((invoice == null) || (invoice.Id == 0))
            {
               // Get Registration DTO from session
               EventRegistrationDTO eventRegistrationDto =
                   (EventRegistrationDTO)Session[EventRegistrationDtoSessionId];
               if (eventRegistrationDto == null)
                  throw new ApplicationException("Session expired - Page refresh not allowed");

               // Build the invoice

               //var person = GetPerson(); // Load Person object
               person = GetPerson(eventRegistrationDto.User.Id); // Load Person object
               if (person == null)
                  throw new Exception("User reference not found"); ;

               Invoice priorInvoice = GetMostRecentInvoice(person.Id, eventRegistrationDto.Event.Id);
               double priorAmt = priorInvoice == null ? 0.0d : priorInvoice.TotalAmount * (-1);
               BackOutInvoice(priorInvoice);

               // Create a new invoice for this registration/purchase
               invoice = RegistrationToInvoice(person, priorAmt, eventRegistrationDto);

               invoiceResult = (InvoiceResult)Session[InvoiceResultSessionId] ?? invoice.Result;
               // If the invoice has an amount due, display the purchase form
               // If the transaction is approved, the purchase form response
               // method will save the invoice with the transaction details.
               if (Math.Abs(invoice.TotalAmount + invoice.PriorCredit) >= .01)
               {
                  purchaseDto = new PurchaseDTO(invoice);
                  if (purchaseDto.EventInfoDto == null)
                     throw new Exception("Invoice or Event not found");

                  return View(purchaseDto);
               }

               // If the invoice has no amount due, create the transaction details,
               // save them to the invoice, and save the invoice.
               invoice.PaymentProcessor = "No Charge";

               invoiceResult.AddDataPair(InvoiceResult.TransactionTimeId,
                   DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
               invoiceResult.AddDataPair(InvoiceResult.TransactionAmountId, "0.00");
               invoiceResult.AddDataPair(InvoiceResult.TransactionUserId, Security.CurrentUserName);
               AddPriorInvoiceReferences(priorInvoice, invoiceResult);
               invoiceResult.AddLogEntry(Security.CurrentUserName, "\"No Charge\" transaction complete");
               invoice.Result = invoiceResult;
               _db.Invoices.AddObject(invoice);
               _db.SaveChanges();
               Session.Remove(InvoiceResultSessionId);
               Session.Remove(EventRegistrationDtoSessionId);
            }

            // If the invoice had an Id when we started or we added it above,
            // we can display the confirmation page.
            return RedirectToAction("Confirmation", new { invoice.Id });
         }
         // Application exceptions can happen but are not really a system problem, like
         // when the user allows their session to expire before they finish something.
         // We inform the user, but don't need to notify the admin or log the error
         catch (ApplicationException ex)
         {
            // Display error to user
            ModelState.AddModelError(String.Empty, ex.Message);
         }
         // Unexpected exceptions are trapped here.  We notify the system administrator and log the error
         catch (Exception ex)
         {
            invoiceResult.AddLogEntry(Security.CurrentUserName, "Payment Error: " + ex.Message);
            ModelState.AddModelError(String.Empty, ex.Message);
            string user = person != null ? person.FullName + "(" + person.Email + ")" : "Unknown";
            string eventDescription = "Unknown";
            if (purchaseDto != null)
               eventDescription = purchaseDto.eventName ?? "Unknown";
            SystemFailureEmail.Send("Payment Response", ex, user, eventDescription);
         }
         return View((PurchaseDTO)null);
      }

      /// <summary>
      /// Processes response form Elavon payment processing.  Purchase payment form tells
      /// Elavon to redirect response from its processing attempt to this URL.
      /// </summary>
      /// <returns>View of purchase page on error; redirect to confirmation page on success</returns>
      [ValidateInput(false)]        // Allows embedded HTML in Elevon response strings (Oh, My!)
      [HttpGet]
      public ActionResult ElavonResponse(ElavonResponseDTO responseDto)
      {
         PurchaseDTO purchaseDto = null;
         InvoiceResult invoiceResult = (InvoiceResult)Session[InvoiceResultSessionId] ?? new InvoiceResult();
         Person person = null;
         try
         {
            // Validate argument and general processing setup

            if (responseDto == null)
               throw new ArgumentException("No transaction data returned");

            // Get Registration DTO from session and confirm it has all needed data
            EventRegistrationDTO eventRegistrationDto =
               (EventRegistrationDTO)Session[EventRegistrationDtoSessionId];

            if (eventRegistrationDto == null)
               throw new Exception("Event Registration information not found in session");

            if (eventRegistrationDto.User == null)
               throw new Exception("Event Registration user information is missing");

            // Get other data for the new invoice
            person = GetPerson(eventRegistrationDto.User.Id); // Load Person object
            if (person == null)
               throw new Exception("Registration user reference [" + eventRegistrationDto.User.Id + "] is invalid");

            Invoice priorInvoice = GetMostRecentInvoice(person.Id, eventRegistrationDto.Event.Id);
            double priorAmt = priorInvoice == null ? 0.0d : priorInvoice.TotalAmount * (-1);

            // Backout the prior invoice.  We do this before we create the new invoice
            // so the inventory totals are recalculated before the new invoice takes
            // away from them.  The changes won't be saved unless we get through all
            // the data checks below and we also save the new invoice.
            BackOutInvoice(priorInvoice);

            // Create a new invoice for this registration / purchase                /
            Invoice invoice = RegistrationToInvoice(person, priorAmt, eventRegistrationDto);
            invoiceResult.AddLogEntry(Security.CurrentUserName, "Payment Response Received from " + responseDto.PaymentProcessor);

            // Setup view data so we can show the purchase form back to the user if necessary
            purchaseDto = new PurchaseDTO(invoice);
            if (purchaseDto.EventInfoDto == null)
               throw new Exception("Event not found");

            // Determine outcome of purchase/refund transaction
            if (responseDto.Success)
            {
               // Transaction OK
               //
               // "Your transaction was approved..." is not really an error, but if some error happens as we
               // save everything, we want the inform the user the transaction succeeded, tell them not to try
               // the transaction again, and tell them to contact customer support.
               purchaseDto.ProcessErrors.Add(
                  string.Format("Your transaction was approved and {0} in the amount of {1:C} but an error occurred saving the information.",
                  (purchaseDto.Amount > 0 ? "payment has been made" : "a refund has been issued"), Math.Abs(purchaseDto.Amount)));
               purchaseDto.ProcessErrors.Add("Do not submit this form again");
               purchaseDto.ProcessErrors.Add("Please contact customer service");
               purchaseDto.SubmitAllowed = false;

               //throw new Exception("Just testing - remove me");

               // Note the payment processor in the invoice
               invoice.PaymentProcessor = responseDto.PaymentProcessor;

               // Clear any existing payment data in the invoice Result object and save the new payment data.
               invoiceResult.ClearData();
               invoiceResult.AddDataPair(InvoiceResult.TransactionUserId, Security.CurrentUserName);
               responseDto.GetResults(invoiceResult);
               AddPriorInvoiceReferences(priorInvoice, invoiceResult);

               // Add a log entry of all the payment details so we can see when an invoice
               // has been paid more than once (should never happen, but...)
               invoiceResult.AddLogEntry(Security.CurrentUserName, "Payment Complete"
                                                                   +
                                                                   invoiceResult.Data.Aggregate("",
                                                                      (current, item) =>
                                                                         current +
                                                                         ("; " + item.Key + "=" + item.Value)));

               // Completed all additions to the InvoiceResult - Save it back to the Invoice object,
               // add the new invoice to the Invoices data set, and save the changes.
               invoice.Result = invoiceResult;
               _db.Invoices.AddObject(invoice);
               _db.SaveChanges();

               // Remove our saved objects from the session
               Session.Remove(InvoiceResultSessionId);
               Session.Remove(EventRegistrationDtoSessionId);

               // Because we are responding to the payment form presented in an HTML iframe
               //
               //     return RedirectToAction("Confirmation", new { invoice.Id });
               //
               // won't work because the target page would be displayed in the iframe.  Instead,
               // we have to make the parent window of the iframe navigate to the confirmation page.
               string content =
                  String.Format(
                     "<html><script>window.top.location.href = '{0}://{1}:{2}/Event/Confirmation/{3}'; </script></html>",
                     Request.IsSecureConnection ? "https" : "http", Request.Url.Host, Request.Url.Port, invoice.Id);
               return Content(content);
            }

            // Payment failed - get reason why, add an entry to the invoice result log, and setup to show to user
            string errorText = responseDto.ErrorText;
            invoiceResult.AddLogEntry(Security.CurrentUserName, "Payment Error: " + errorText);
            purchaseDto.ProcessErrors.Add(errorText);
         }
         catch (Exception ex)
         {
            // Unexpected exceptions are trapped here.  We notify the system administrator and log the error
            invoiceResult.AddLogEntry(Security.CurrentUserName, "Processing Error: " + ex.Message);
            ModelState.AddModelError(String.Empty, ex.Message);
            string user = person != null ? person.FullName + "(" + person.Email + ")" : "Unknown";
            string eventDescription = "Unknown";
            if (purchaseDto != null)
               eventDescription = purchaseDto.eventName ?? "Unknown";
            SystemFailureEmail.Send("Payment Response", ex, user, eventDescription);
         }
         SaveToSession(InvoiceResultSessionId, invoiceResult);
         return View("PurchaseForm", purchaseDto);
      }


      [HttpGet]
      [Authorize]
      public ActionResult Confirmation(int id)
      {
         EventConfirmationDTO eventConfirmationDto = null;
         try
         {
            // Confirm Request is valid
            if ((Request == null) || (Request.Url == null))
               throw new Exception("Request failure");
            string protocol = Request.IsSecureConnection ? "https" : "http";

            //  Add the back button reference - at least the user can get back to the main event list
            ViewBag.BackRef = String.Format("{0}://{1}:{2}/Event", protocol, Request.Url.Host, Request.Url.Port);

            // Get the data needed and construct the DTO and confirm we have all the needed data
            Invoice invoice = GetInvoice(id);
            if (invoice == null)
               throw new ApplicationException("Registration information not found");

            if (invoice.Store == null)
               throw new Exception("Invoice missing event reference");

            if (invoice.Purchaser == null)
               throw new Exception("Invoice missing purchaser reference");

            // Log this action in the invoice actions log, but don't save it yet
            // in case something fails and the confirmation is not displayed.
            // We do this here so we get nice formatting of the payment result
            // item Ids done by the EventConfirmationDTO constructor.
            InvoiceResult result = invoice.Result;
            if (!result.Locked)
            {
               result.AddLogEntry(Security.CurrentUserName, "Confirmation Form Presented");
               invoice.Result = result;
            }
            // Build the DTO
            eventConfirmationDto = new EventConfirmationDTO(invoice, invoice.Purchaser);
            if (eventConfirmationDto.EventInfoDto == null)
               throw new Exception("Event information not found");

            // If the user is not a system administrator, set the InvoiceResult of the DTO to null
            // so the invoice purchase details and action log are not displayed on the confirmation
            // page.
            if (!Roles.IsUserInRole("Administrator"))
               eventConfirmationDto.InvoiceDto.Result = null;

            // Add button references for matchmaking and registration change/update
            if (eventConfirmationDto.EventInfoDto.IsMatchmakingEvent)
               ViewBag.MatchRef = String.Format("{0}://{1}:{2}/MatchMaker/Matchmaker/{3}?uId={4}",
                   protocol, Request.Url.Host, Request.Url.Port, invoice.Store.Id, invoice.Purchaser.Id);

            ViewBag.ChangeRef = String.Format("{0}://{1}:{2}/Event/EventRegistration/{3}?uId={4}",
                protocol, Request.Url.Host, Request.Url.Port, invoice.Store.Id, invoice.Purchaser.Id);

            // Everything OK - Lock the invoice log and save the invoice 
            if (!result.Locked)
            {
               result.Lock();     // Prevents further log entries
               invoice.Result = result;
               _db.SaveChanges();
            }
         }
         // Application exceptions are trapped here - we dont send an E-mail or make a log entry.
         // In this case, if the invoice Id does not find an invoice we inform the user but
         // don't care much otherwise because a user could manually type the Id into the URL
         // and its unlikely our references (like from the event list pages) will be wrong.
         catch (ApplicationException ex)
         {
            ModelState.AddModelError(String.Empty, ex.Message);
         }
         // Unexpected exceptions are trapped here.  We notify the system administrator and log the error
         catch (Exception ex)
         {
            ModelState.AddModelError(String.Empty, ex.Message);
            string eventDescription = "Unknown";
            string user = "Unknown";
            if (eventConfirmationDto != null)
            {
               if (eventConfirmationDto.User != null)
                  user = eventConfirmationDto.User.FirstName + " " + eventConfirmationDto.User.LastName
                      + " (" + eventConfirmationDto.User.Email + ")";
               if (eventConfirmationDto.EventInfoDto != null)
                  eventDescription = eventConfirmationDto.EventInfoDto.Name ?? "Unknown";
            }
            SystemFailureEmail.Send("Event Confirmation", ex, user, eventDescription);
         }

         return View(eventConfirmationDto);
      }

      #endregion

      #region Matchmaking Scheduling Methods

      [HttpGet, ActionName("ScheduleMeetings")]
      [Authorize]
      public ActionResult ScheduleMeetings(int id = 0)
      {
         return View(DetailsEventInfo(id));
      }


      [HttpGet, ActionName("AutoSchedule")]
      public ActionResult AutoSchedule(int id = 0)
      {
         return View(DetailsEventInfo(id));
      }

      [HttpPost]
      [ValidateAntiForgeryToken]
      [Authorize]
      public ActionResult AutoSchedule(EventInfo eventinfo)
      {
         return RedirectToAction("AutoSchedule", new { eventinfo.Id });
      }

      [HttpGet, ActionName("ScheduleConfirmation")]
      [Authorize]
      public ActionResult ScheduleConfirmation(int id = 0)
      {
         return View(DetailsEventInfo(id));
      }

      [HttpPost]
      [ValidateAntiForgeryToken]
      [Authorize]
      public ActionResult ScheduleConfirmation(EventInfo eventinfo)
      {
         return RedirectToAction("ScheduleConfirmation", new { eventinfo.Id });
      }
      #endregion

      #region E-Mail Methods

      private void SendConfirmationEmail(Invoice invoice, Dictionary<string, string> emailIds)
      {
         dynamic mail = new Email("EventConfirmation");
         string to = String.Join(",", emailIds.Select(x => x.Key));
         //string to = String.Join(",", emailIds.Select(x => (x.Value + "<" + x.Key + ">")));
         mail.RecipientsTo = to;
         mail.From = Security.CurrentUserName;
         mail.Subject = "Check Ohio First - Event Confirmation";
         mail.EventConfirmationDto = new EventConfirmationDTO(invoice, invoice.Purchaser);
         mail.Send();
      }

      [HttpPost]
      //[HandleJsonException]
      //[MyError]
      [Authorize]
      public JsonResult SendConfirmationEmail(int id)
      {
         try
         {
            // Confirm inputs are good
            if (id <= 0)
               throw new ApplicationException("Invalid invoice Id [" + id + "]");

            Invoice invoice = GetInvoice(id);
            if (invoice == null)
               throw new Exception("Invoice not found for Id [" + id + "]");

            if (invoice.LineItems.Count == 0)
               throw new ApplicationException("Invoice has no content");

            // Build the list of unique E-mail Ids from the invoice line items
            // and the invoice purchaser
            Dictionary<string, string> emailIds = new Dictionary<string, string>();

            // Purchaser always gets an E-mail if we have the information
            if ((invoice.Purchaser != null) && (!String.IsNullOrEmpty(invoice.Purchaser.Email)))
               emailIds.Add(invoice.Purchaser.Email, invoice.Purchaser.FullName);

            foreach (LineItem lineItem in invoice.LineItems)
            {
               if (lineItem.Personalizations != null)
               {
                  foreach (Person person in lineItem.Personalizations)
                  {
                     if (!emailIds.ContainsKey(person.Email))
                        emailIds.Add(person.Email, person.FullName);
                  }
               }
            }

            // If no addresses, we can't send an E-mail (we SHOULD always at least have the purchaser)
            if (emailIds.Count == 0)
               throw new Elmah.ApplicationException("No addresses found for E-mail");

            SendConfirmationEmail(invoice, emailIds);

            // Use a Dictionary to construct what will be sent as a Json object with
            // ErrorCode, ErrorMessage, and Data properties.
            //
            // Data property is sent as an array of strings in the form
            //
            //     User full name (user E-mail)
            //
            Dictionary<string, object> jsonResponse = new Dictionary<string, object>();
            jsonResponse.Add("ErrorCode", 0);
            jsonResponse.Add("ErrorMessage", "SUCCESS");
            jsonResponse.Add("Data", emailIds.Select(x => (x.Value + " (" + x.Key + ")")).ToArray());
            return Json(jsonResponse);
         }
         catch (Exception ex)
         {
            // The correct thing to do here would be to set the HTTP request object
            // to internal server error and then send the response.  But when we do
            // that the AJAX call in the Javascript client completes without
            // triggering the "fail" function.
            //
            // So, we just stuff the error details into a conventional Json object
            // and inspect these in the success function of the AJAX call.
            Dictionary<string, object> jsonResponse = new Dictionary<string, object>();
            jsonResponse.Add("ErrorCode", -1);
            jsonResponse.Add("ErrorMessage", ex.Message);
            return Json(jsonResponse);
         }
      }

      //
      // GET: /ErdtoEvent/Eventregistration/5
      [HttpGet]
      [Authorize]
      public ActionResult WebinarRequest()
      {
         var eMailDto = new EMailDTO { Title = "Webinar Request" };
         if (Request == null || Request.UrlReferrer == null)
         {
            eMailDto.UrlReferrer = "";
         }
         else
         {
            eMailDto.UrlReferrer = Request.UrlReferrer.ToString();
         }

         return View(eMailDto);
      }

      [HttpPost]
      [ValidateAntiForgeryToken]
      [Authorize]
      public ActionResult WebinarRequest(EMailDTO eMailDto)
      {
         if (ModelState.IsValid)
         {
            /*
             * Get user information to retrieve E-mail address of user and admin
             */
            var user = GetPerson();
            //var request = this.Request;

            if (user == null)
            {
               ModelState.AddModelError("", "You must be logged in to send mail");
               return View(eMailDto);
            }

            var userAddress = new MailAddress(user.Email, user.FullName);

            try
            {
               var adminName = ConfigurationManager.AppSettings["Admin.DisplayName"];
               if (String.IsNullOrEmpty(adminName))
                  throw new Exception("System administrator name is not defined in the application configuration.");

               var adminAddr = ConfigurationManager.AppSettings["Admin.EMail"];
               if (String.IsNullOrEmpty(adminAddr))
                  throw new Exception("System administrator E-mail address is not defined in the application configuration.");

               var adminAddress = new MailAddress(adminAddr, adminName);
               /*
                *  Create E-mail message using System.Net.Mail api
                */
               var msg = new MailMessage
               {
                  DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure,
                  From = userAddress,
                  Subject = "Webinar Request - " + eMailDto.Title,
                  Body = "Description:\n\n"
                         + eMailDto.Description
                         + "\n\n"
                         + "Start date & time:\t" + eMailDto.StartDate + " " + eMailDto.StartTime
                         + "\n\n"
                         + "End date & time:\t" + eMailDto.EndDate + " " + eMailDto.EndTime
                         + "\n\n"
                         + "Expected Participants:\t" + eMailDto.Participants
               };

               msg.To.Add(adminAddress);
               msg.CC.Add(userAddress);

               SmtpClient.Send(msg);
               return RedirectToRoute(new { controller = "Company", action = "Dashboard" });
            }
            catch (Exception e)
            {
               ModelState.AddModelError(String.Empty,
                   "Unable to send message.  Try again, and if the problem persists, contact our help desk.");
               ModelState.AddModelError(String.Empty, e.Message);
            }
         }
         return View(eMailDto);
      }

      [HttpGet]
      public ActionResult HelpRequest()
      {
         var eMailHelpDto = new EMailHelpDTO();
         var user = GetPerson();
         if (user != null)
         {
            eMailHelpDto.UserEMail = user.Email;
            eMailHelpDto.UserName = user.FullName;
         }
         eMailHelpDto.Title = "Help Request";
         eMailHelpDto.UrlReferrer = ((Request != null) && (Request.UrlReferrer != null))
             ? Request.UrlReferrer.ToString()
             : "";

         ViewBag.Categories = new SelectList(
             new[]
                {
                    new {Id = "How-to & General Help", Category = "How-to & General Help"},
                    new {Id = "Registration", Category = "Registration"},
                    new {Id = "Data Problems", Category = "Data Problems"},
                    new {Id = "Usage & Application Errors", Category = "Usage & Application Errors"},
                    new {Id = "Other", Category = "Other"}
                },
             "Id", "Category", "How-to & General Help");

         return View(eMailHelpDto);
      }

      [HttpPost]
      [ValidateAntiForgeryToken]
      [Authorize]
      public ActionResult HelpRequest(EMailHelpDTO eMailHelpDto)
      {
         if (ModelState.IsValid)
         {
            /*
             * Get user information to retrieve E-mail address of user and admin
             */
            var errorFlag = false;
            var user = GetPerson();
            var userEmail = ((eMailHelpDto.UserEMail.Trim() != "")
                ? eMailHelpDto.UserEMail.Trim()
                : (user != null ? user.Email : null));

            var userName = ((eMailHelpDto.UserName.Trim() != "")
                ? eMailHelpDto.UserName.Trim()
                : (user != null ? user.FullName : null));

            if (userEmail == null)
            {
               ModelState.AddModelError("", "E-Mail address is required");
               errorFlag = true;
            }

            if (userName == null)
            {
               ModelState.AddModelError("", "Name is required");
               errorFlag = true;
            }

            if (eMailHelpDto.Description.Trim() == "")
            {
               ModelState.AddModelError("", "Description is required");
               errorFlag = true;
            }

            if (errorFlag)
               return View(eMailHelpDto);

            var userAddress = new MailAddress(userEmail, user.FullName);


            try
            {
               string adminName = ConfigurationManager.AppSettings["HelpDesk.DisplayName"];
               string adminAddr = ConfigurationManager.AppSettings["HelpDesk.EMail"];
               MailAddress adminAddress = new MailAddress(adminAddr, adminName);
               /*
                *  Create E-mail message using System.Net.Mail api
                */
               var msg = new MailMessage
               {
                  DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure,
                  From = userAddress,
                  Subject = ((eMailHelpDto.Title.ToUpper().StartsWith("HELP REQUEST") ? "" : "Help Request - ")) +
                            eMailHelpDto.Title,
                  Body = "Category: " + eMailHelpDto.Category
                      + "\n\n"
                      + "Description:\n\n"
                      + eMailHelpDto.Description
               };

               msg.To.Add(adminAddress);
               msg.CC.Add(userAddress);

               /*
                * Create SMTP client
                */
               var sc = new SmtpClient { Host = "localhost", Port = 25 };
               sc.Send(msg);
               //return RedirectToAction("Index");
               return RedirectToRoute(new { controller = "Company", action = "Dashboard" });
            }
            catch (Exception e)
            {
               ModelState.AddModelError(String.Empty,
                   "Unable to send message.  Try again, and if the problem persists, contact our help desk.");
               ModelState.AddModelError(String.Empty, e.Message);
            }
         }
         return View(eMailHelpDto);
      }




      #endregion

      #region EventRegistration

      [Authorize]
      public ActionResult RegisteredCompany(int id)
      {

         var ei = _db.Stores.OfType<EventInfo>()           // Load Event object
             .Include("Roles")
             .Include("Inventory")
             .SingleOrDefault(e => e.Id == id);

         if (ei == null)
            return HttpNotFound("Event record not found");

         ViewBag.Title = "List of Companies Registered for the Event - " + ei.Name;

         //get all invoices for selected event
         List<Invoice> invoicesCompany = _db.Invoices
             .Include("Purchaser.Employer")
             .Where(i => i.Store.Id == id)
             .ToList();

         return View(invoicesCompany);
      }


      //[Authorize]
      //public ActionResult EventPurchaseList(int id, int pid, int regType =0)
      //{

      //    var ei = _db.Stores.OfType<EventInfo>()           // Load Event object
      //        .Include("Roles")
      //        .Include("Inventory")
      //        .SingleOrDefault(e => e.Id == id);

      //    if (ei == null)
      //        return HttpNotFound("Event record not found");


      //    var person = _db.People.Include("Employer").FirstOrDefault(p => p.Id == pid);

      //    if (person == null)
      //        return HttpNotFound("Person not found");

      //    List<Invoice> invoices = _db.Invoices
      //        .Include("Purchaser")
      //        .Include("LineItems.Personalizations")
      //        .Where(i => i.Store.Id == id && i.Purchaser.Id==pid)
      //        .ToList();

      //    Invoice priorInvoice = invoices.FirstOrDefault(i => i.Date == invoices.Max(j => j.Date));

      //    if (priorInvoice == null)
      //        return HttpNotFound("Person not found");

      //    List<LineItem> lineItems = priorInvoice.LineItems.Where(li => li.Personalizations != null).ToList();

      //    List<Person> attendees = lineItems.SelectMany(li => li.Personalizations).ToList();

      //     ViewBag.Title = "Attendees for the event - " + ei.Name + "from " + person.Employer.Name;
      //     return View(Mapper.Map<List<Person>, List<CheckOhioFirst.Web.Models.PersonDTO>>(attendees));
      //}

      #endregion

   }
}