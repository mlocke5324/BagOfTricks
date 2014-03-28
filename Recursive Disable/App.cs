using System;
using SharpKit.Html;
using SharpKit.JavaScript;
using SharpKit.jQuery;

[JsType(JsMode.Prototype, Filename = "Script\\App.js")]
public class App : jQueryContext
{
   #region Class Variables, Constants, & Constructors

   private const int GetNotify_WaitOnErrorMilliseconds = 2000;
   public static JsString Version = "1.0";
   private static readonly string[] pages = { "home", "emergency", "heatcool", "pm", "tools" };
   private static bool _isPaused;

   public App()
   {
      //connectionValidated = false;
      _isPaused = false;
   }

   #endregion

   #region Current state

   private static bool IsPaused
   {
      get { return _isPaused; }
      set
      {
         if (_isPaused != value)
         {
            _isPaused = value;
            PollForNotifications();
         }
      }
   }

   private static bool IsOnline
   {
      //get { return Helper.IsOnline(); }
      get { return navigator.onLine; }
   }

   private static Configuration CurrentConfig
   {
      get { return Configuration.Current; }
   }

   private static bool IsLoggedIn
   {
      get { return CurrentConfig != null && !CurrentConfig.IsExpired; }
   }

   private static bool IsVehicleSelected
   {
      get { return CurrentConfig != null && CurrentConfig.CurrentVehicle != null; }
   }

   private static bool HasPMData
   {
      get { return IsVehicleSelected && CurrentConfig.CurrentVehicleData.PMReport != null; }
   }

   private static bool AreCommandButtonsAvailable
   {
      get
      {
         return IsVehicleSelected && (AvailableFeatureIDs.length != 0) /*&& connectionValidated*/;
      }
   }

   //private static bool connectionValidated;

   private static JsArray<int> AvailableFeatureIDs
   {
      get
      {
         JsArray<int> featureIDs = new JsArray<int>();
         if (CurrentConfig != null &&
             CurrentConfig.CurrentVehicleData != null &&
             CurrentConfig.CurrentVehicleData.FeatureIDs != null)
         {
            for (int index = 0; index < CurrentConfig.CurrentVehicleData.FeatureIDs.Length; index++)
               featureIDs.Add(CurrentConfig.CurrentVehicleData.FeatureIDs[index]);
         }
         return featureIDs;
      }
   }

   #endregion

   public static void Init()
   {
      //Helper.SplashScreen(true);
      
      try
      {
         //connectionValidated = false;
         jQuery.ajaxSetup(new AjaxSettings{timeout = 2000});

         // application
         document.addEventListener("pause", evt => { IsPaused = true; }, false);
         document.addEventListener("resume", evt => { IsPaused = false; }, false);


         // all pages
         J(document).on("click", "div[data-feature-cmd] button", null, CommandButton_click);
         J(document).on("click", "div[data-feature-cmd] a", null, CommandButton_click);
         J(document).on("swiperight", "[data-role='page']", null, Page_swiperight);
         J(document).on("swipeleft", "[data-role='page']", null, Page_swipeleft);

         // signon
         J(document).on("click", "#loginBtn", null, LoginBtn_click);
         J(document).on("backbutton", "#signon", null, Signon_backbutton);

         // Change Password
         J(document).on("click", "#changePasswordButton", null, ChangePasswordBtn_click);

         // tools
         J(document).on("click", "#connectBtnWrapper", null, ConnectBtn_click);
         J(document).on("click", "#changeTruckDlg li a", null, OnChooseTruck);
         J(document).on("click", "#logoutBtn", null, LogoutBtn_click);

         // home
         J(document).on("slidestop", "#lockEngine", null, EngineLock_slidestop);
         //J(document).on("pageinit", "#home", true, SetupEngine);
         J(document).on("pagebeforeshow", "#home", null, LockEngine);

         if (!IsLoggedIn)
         {
            Helper.SlidePage("#signon", "R");
            return;
         }

         CreateVehicleSelectionList();
         DimAllFeatures(true);
         DimAllPM(true);

         if (IsVehicleSelected)
         {
            ConnectToSelectedVehicle();
            Helper.SlidePage("#home", "R");
            Helper.LoadingDlg("show");
         }
         else
         {
            UpdateVehicleConnectionStatusDisplay(false);
            Helper.SlidePage("#tools", "L");
         }
      }
      finally
      {
         //Helper.SplashScreen(false);
      }
   }

   private static void Signon_backbutton(Event arg)
   {
      Helper.Exit();
   }

   #region Page change

   private static void Page_swipeleft(Event arg)
   {
      HtmlElement element = arg.target;
      int index = jQuery.inArray(element.id, pages);

      while (index == -1 && element.parentElement != null)
      {
         element = (HtmlElement)element.parentElement;
         index = jQuery.inArray(element.id, pages);
      }

      if (element.parentElement != null && element.id != "signon")
      {
         if (index < pages.Length - 1)
            Helper.SlidePage("#" + pages[index + 1], "L");
      }
   }

   private static void Page_swiperight(Event arg)
   {
      HtmlElement element = arg.target;
      int index = jQuery.inArray(element.id, pages);

      while (index == -1 && element.parentElement != null)
      {
         element = (HtmlElement)element.parentElement;
         index = jQuery.inArray(element.id, pages);
      }

      if (element.parentElement != null && element.id != "signon")
      {
         if (index > 0)
            Helper.SlidePage("#" + pages[index - 1], "R");
      }
   }

   private static void LockEngine(Event arg)
   {
      Helper.SetEngineSlider(J("#lockEngine"), "on");
   }

   #endregion Page change

   #region Listen for change notifications

   private static void PollForNotifications()
   {
      if (!IsPaused && IsOnline && IsVehicleSelected)
      {
         CurrentConfig.CurrentVehicle.GetNotify(GetNotify_success, GetNotify_error, null);
      }
   }

   private static void GetNotify_success(object arg1, JsString arg2, jqXHR arg3)
   {
      ProcessStatus(arg1, arg2, arg3);
      PollForNotifications();
   }

   private static void GetNotify_error(jqXHR arg1, JsString arg2, JsError arg3)
   {
      if (arg2 == "timeout")
      {
         PollForNotifications();
      }
      else
      {
         UpdateVehicleConnectionStatusDisplay(false);
      }
   }

   #endregion Listen for change notifications

   #region Button management

   private static void DimAllPM(bool dim)
   {
      dim = !HasPMData || dim;
      J("[data-pm-feature]").each((number, element) => Dim(J(element), dim));
   }

   private static void DimAllFeatures(bool dim)
      {
      J("[data-feature-id]").each((number, element) => Dim(J(element), dim));
      }

   // DimByStatusId and DimElementAndChildren create a recursive loop to dim
   // elements that have a given data-status-id and any children of those
   // elements.  Recursion stops when an element's data-feature-id is not
   // found as the data-status-id of another element.
   //
   // WARNING - THERE IS NO PROTECTION FROM A CIRCULAR REFERENCE - WARNING
   //
   // No parent element may have a data-status-id that is the data-feature-id
   // of any of its decendents.  If so, recursion will not stop and a stack
   // overflow will result.

   private static void DimByStatusId(string statusId, bool dim)
   {
      if ((statusId == null) || (statusId == ""))
         return;

      // Dim all elements that have this status Id and children of those elements
      J("[data-status-id=" + statusId + "]").each((number, element) => DimElementAndChildren(J(element), dim));
   }

   private static void DimElementAndChildren(jQuery element, bool dim)
   {
      Dim(J(element), dim);

      // Dim children of this element - any elements that have this element's feature Id as their status Id
      DimByStatusId(J(element).attr("data-feature-id").trim(), dim);
   }

   /// <summary>
   /// For elements that have a data-status-id attribute that references an
   /// element with a boolean-type data-cmd-type, determine if the parent
   /// element is in an "Off" state or is disabled.  If so, return true
   /// indicating the child should not be enabled because the status of
   /// the parent does not allow it.
   /// </summary>
   /// <param name="element">element to be checked</param>
   /// <returns>True - Don't allow enable of element; False - Enable of element is OK</returns>
   private static bool ParentBlocksEnable(jQuery element)
   {
      string dataStatusId = J(element).attr("data-status-id");
      if (dataStatusId == null || dataStatusId == "")
         return false;

      jQuery parentElement = J("[data-feature-id=" + dataStatusId + "]");
      string commandType = J(parentElement).attr("data-cmd-type").trim().toUpperCase();
      if ((commandType != "B") & (commandType != "!B"))
         return false;

      bool parentOff = (J(parentElement).attr("data-feature-value").trim().toUpperCase() == (commandType == "B" ? "0" : "1"));
      return (parentOff || J(parentElement).hasClass("ui-disabled"));
   }

   /// <summary>
   /// Enable or disable an element
   /// 
   /// An element may not be enabled if its parent's status disallows it.
   /// </summary>
   /// <param name="element">Element to be enabled/disabled</param>
   /// <param name="dim">True - disable element (make it dim); False - enable element (make it not dim)</param>
   private static void Dim(jQuery element, bool dim)
   {
      if (dim || ParentBlocksEnable(element))
         J(element).addClass("ui-disabled");
      else
            J(element).removeClass("ui-disabled");
   }

   private static void HideMissingFeatures()
   {
      // show all the controls
      J("[data-feature-id]").show();

      // get the available features, if connected
      JsArray<int> featureIDs = AvailableFeatureIDs;

      // if there aren't any features
      if (featureIDs.length == 0)
      {
         ShowAllFeatures();
         DimAllFeatures(true);
         DimAllPM(true);
      }
      else
      {
         // match them up with the feature ids from the vehicle. If not present, hide the control
         J("[data-feature-id]").each(
                                     delegate(JsNumber number, HtmlElement element)
                                     {
                                        jQuery jqDiv = J(element);

                                        JsNumber featureId = parseInt(jqDiv.attr("data-feature-id"));
                                        bool available = (jQuery.inArray(featureId, featureIDs) != -1);

                                        if (!available)
                                           jqDiv.hide();
                                        else
                                           jqDiv.show();
                                     });
      }
   }

   private static void ShowAllFeatures()
   {
      J("[data-feature-id]").each((number, element) => J(element).show());
   }

   private static void EngineLock_slidestop(Event arg)
   {
      bool dim = (J("#lockEngine").val() == "on");
      Dim(J("[data-feature-cmd='999']"), dim);
   }

   #endregion Button management

   #region ChangePassword

   public static void ClearControls()
   {
      J("#OldPassword").val("");
      J("#NewPassword").val("");
      J("#ConfirmPassword").val("");
      J("#changePasswordError").html("");
   }

   public static void ChangePasswordBtn_click(Event arg)
   {
      JsString email = Configuration.Current.CurrentUser;
      JsString oldpassword = J("#OldPassword").val().As<JsString>();
      JsString newpassword = J("#NewPassword").val().As<JsString>();
      JsString confirmpassword = J("#ConfirmPassword").val().As<JsString>();


      if (newpassword == confirmpassword)
      {
         if (oldpassword != "" && newpassword != "" && confirmpassword != "")
         {
            if (confirmpassword == oldpassword)
            {
               AddChangePasswordMessage("Old Password and New Password Can't be same");
            }
            else
            {
               CommandCenter.ChangePassword(
                   email, oldpassword, newpassword, ChangePasswordSuccess,
                   (xhr, s, arg3) => AddChangePasswordMessage("Error communicating with Command Center"));
            }
         }
         else
         {
            AddChangePasswordMessage("Enter all required fields");
         }
      }
      else
      {
         AddChangePasswordMessage("Passwords don't match");
      }
   }

   private static void ChangePasswordSuccess(object o, JsString jsString, jqXHR arg3)
   {
      bool success = o.As<bool>();

      if (success)
      {
         ClearControls();
         Helper.ClosePopup("#changePasswordDlg");
      }
      else
      {
         AddChangePasswordMessage("Error changing password");
      }
   }

   private static void AddChangePasswordMessage(JsString message)
   {
      J("#changePasswordError").html("<li style='margin-left:-2.4em; text-align:left;'>" + message + "</li>");
   }

   #endregion

   #region Login/Logout

   private static void LogoutBtn_click(Event arg)
   {
      if (confirm("Logout - are you sure?"))
      {
         Configuration.Clear();
         Helper.SlidePage("#signon", "L");
      }
   }

   public static void LoginBtn_click(Event arg)
   {
      J("#loginMessages").html("");

      JsString username = J("#userName").val().As<JsString>();
      JsString password = J("#password").val().As<JsString>();

      if (username == null || username == "" || password == null || password == "")
      {
         AddLoginMessage("User name and password are required");
         return;
      }

      Helper.LoadingDlg("show");
      CommandCenter.Login(
                          username, password, LoginSuccess,
                          (xhr, s, arg3) => AddLoginMessage("Error communicating with Command Center server: " + s));
   }

   private static void LoginSuccess(object o, JsString jsString, jqXHR arg3)
   {
      Helper.LoadingDlg("hide");
      ConfigInfo configInfo = o.As<ConfigInfo>();

      if (configInfo.LoginSuccess)
      {
         JsArray<int> ids = new JsArray<int>();
         JsArray<PMReport> reports = new JsArray<PMReport>();
         Configuration config = Configuration.Current ?? Configuration.GetCachedConfig();

         if (config != null)
         {
            for (int index = 0; index < config.Vehicles.Length; index++)
            {
               VehicleData data = config.Vehicles[index];

               if (data.PMReport != null && !data.LastPMReportUploaded)
               {
                  CommandCenter.SendPMData(data);
                  ids.Add(data.Id);
                  reports.Add(data.PMReport);
               }
            }
         }

         JsString username = J("#userName").val().As<JsString>();
         Configuration.Create(username, configInfo);
         Configuration.Current.RememberMe = J("#rememberMe").prop("checked").As<JsBoolean>();

         for (int index = 0; index < ids.length; index++)
         {
            int id = ids[index];
            PMReport report = reports[index];

            for (int index2 = 0; index2 < CurrentConfig.Vehicles.Length; index2++)
            {
               VehicleData vehicleData = CurrentConfig.Vehicles[index2];
               if (vehicleData.Id == id)
               {
                  vehicleData.PMReport = report;
                  vehicleData.LastPMReportUploaded = true;
                  break;
               }
            }
         }

         // setup engine lock
         //Helper.SetEngineSlider(J("#lockEngine"), "on");

         //connectionValidated = false;
         HideMissingFeatures();
         UpdateVehicleConnectionStatusDisplay(false);
         UpdatePMDisplay();
         CreateVehicleSelectionList();
         Helper.SlidePage("#tools", "L");
      }
      else
      {
         for (int index = 0; index < configInfo.Messages.Length; index++)
         {
            JsString message = configInfo.Messages[index];
            AddLoginMessage(message);
         }
      }
   }

   private static void AddLoginMessage(JsString message)
   {
      Helper.LoadingDlg("hide");
      J("#loginMessages").append("<li style='margin-left:-2.4em; text-align:left;'>" + message + "</li>");
   }

   #endregion Login/Logout

   #region Choose truck

   private static void CreateVehicleSelectionList()
   {
      Dim(J("#connectBtn"), IsVehicleSelected);

      jQuery trucklist = J("#changeTruckDlg ul");
      trucklist.html("");
      JsString trucklistHtml = "";

      VehicleData[] vehicleData = CurrentConfig.Vehicles;
      if (vehicleData != null)
      {
         for (int index = 0; index < vehicleData.Length; index++)
         {
            VehicleData data = vehicleData[index];
            trucklistHtml +=
                  "<li><a href='#' data-prod='" + data.ProductionNumber + "' data-vehicleIndex='" + index +
                  "'>" + data.ShortName + "</a></li>";
         }
      }
      Helper.SetListview(trucklist, trucklistHtml);
   }

   public static void OnChooseTruck(Event arg)
   {
      jQuery target = J(arg.target);
      int index = parseInt(target.attr("data-vehicleIndex"));
      CurrentConfig.SelectTruck(index);

      UpdateVehicleConnectionStatusDisplay(false);
      UpdatePMDisplay();
      ConnectToSelectedVehicle();

      Helper.ClosePopup("#changeTruckDlg");
   }

   #endregion Choose truck

   #region Connect to vehicle

   public static void ConnectBtn_click(Event arg)
   {
      ConnectToSelectedVehicle();
   }

   private static void ConnectToSelectedVehicle()
   {
      CurrentConfig.CurrentVehicle.Capabilities(CapabilitiesRequestSuccess, VehicleCommunicationError);
   }

   private static void CapabilitiesRequestSuccess(object o, JsString s, jqXHR arg4)
   {
      //connectionValidated = true;
      CurrentConfig.CurrentVehicleData.FeatureIDs = o.As<int[]>();
      CurrentConfig.Save();

      HideMissingFeatures();
      UpdateVehicleConnectionStatusDisplay(true);

      CurrentConfig.CurrentVehicle.GetStatus(ProcessStatus, VehicleCommunicationError);
      CurrentConfig.CurrentVehicle.GetPMData(GetPMDataSuccess, VehicleCommunicationError);
      PollForNotifications();
      Helper.LoadingDlg("hide");
   }

   private static void GetPMDataSuccess(object o, JsString jsString, jqXHR arg3)
   {
      //connectionValidated = true;
      UpdateVehicleConnectionStatusDisplay(true);

      PMReport report = o.As<PMReport>();
      CurrentConfig.CurrentVehicleData.PMReport = report;
      CurrentConfig.CurrentVehicleData.LastPMReportUploaded = false;
      CurrentConfig.Save();

      UpdatePMDisplay();
   }

   private static void UpdatePMDisplay()
   {
      if (!IsVehicleSelected || !HasPMData)
      {
         ClearPMDisplayAndDim();
         return;
      }

      const double warnPercent = 0.2d;

      if (HasPMData)
      {
         PMReport report = CurrentConfig.CurrentVehicleData.PMReport;

         if (report.engineHours == -1)
            J("[data-feature='EngineHours']").hide();
         else
            J("[data-feature='EngineHours']").show();

         if (report.mileage == -1)
            J("[data-feature='Mileage']").hide();
         else
            J("[data-feature='Mileage']").show();

         J("#engineHours").val(new JsString(report.engineHours));
         J("#mileage").val(new JsString(report.mileage));

         string newData = "";

         for (int index = 0; index < report.pmValues.Length; index++)
         {
            PMValue curValues = report.pmValues[index];

            if (curValues.value < 0)
            {
               newData += "<div class='pm-messages center' style='margin-bottom: 3px;'>";
               newData += curValues.desc;
               newData += "<div class='pm-messages-bad'>";
               newData += "Overdue by " + new JsString((curValues.value * -1)) + " " + curValues.units;
               newData += "</div></div>";
            }
         }

         if (newData == "")
         {
            newData = "<div class='pm-messages center'>No current messages</div>";
            J("#pmMsgHead").css("color", "#2f292a");
         }
         else
         {
            J("#pmMsgHead").css("color", "red");
         }

         J("#pmMessages-data").html(newData);
         newData = "";
         int maxSeverity = 0;

         for (int index = 0; index < report.pmValues.Length; index++)
         {
            PMValue curValues = report.pmValues[index];

            newData += "<div class='clearfix pm-messages'>";
            newData += "<div class='pm-messages-left'>";
            newData += curValues.desc;
            newData += "</div><div class='pm-messages-right'>";
            newData += new JsString(curValues.interval) + " " + curValues.units;

            if (curValues.value < 0)
            {
               newData += "<div class='pm-messages-bad'>";
               newData += "Overdue by " + new JsString(curValues.value * -1) + " " + curValues.units;
               maxSeverity = 2;
            }
            else if (curValues.value < (curValues.interval * warnPercent))
            {
               newData += "<div class='pm-messages-caution'>";
               newData += "Due in " + new JsString(curValues.value) + " " + curValues.units;
               if (maxSeverity == 0) maxSeverity = 1;
            }
            else
            {
               newData += "<div class='pm-messages-good'>";
               newData += "Due in " + new JsString(curValues.value) + " " + curValues.units;
            }

            newData += "</div></div></div>";
         }

         if (newData == "")
         {
            newData = "<div class='pm-messages center'>No current schedule</div>";
         }

         J("#pmSchedule-data").html(newData);

         if (maxSeverity == 2)
            J("#pmSchedHead").css("color", "red");
         else if (maxSeverity == 1)
            J("#pmSchedHead").css("color", "#FF8205");
         else
            J("#pmSchedHead").css("color", "#2f292a");

         DimAllPM(false);
      }
   }

   private static void ClearPMDisplayAndDim()
   {
      J("#engineHours").val("");
      J("#mileage").val("");
      J("#pmMessages-data").html("");
      J("#pmSchedule-data").html("");

      DimAllPM(true);
   }

   #endregion Connect to vehicle

   #region Send command to vehicle

   public static void CommandButton_click(Event arg)
   {
      /********************************************
       * 
       * data-feature-id = attribute of div surrounding the feature display elements. Indicates entire feature's UI
       * data-feature-cmd = attribute of div surrounding buttons that send commands
       * data-cmd-type = attribute of div surrounding buttons that send commands
       * data-feature-value = attribute of div surrounding elements that have values (including buttons that send commands)
       * 
       * Note: buttons themselves have no data-feature attributes on them
       * 
       * ******************************************/

      Vehicle currentVehicle = (CurrentConfig != null ? CurrentConfig.CurrentVehicle : null);

      if (currentVehicle != null)
      {
         jQuery jqDiv = J(arg.target);

         // the target isn't the div we want; it's likely a button. Work up the DOM to find the div with a data-feature-cmd attr
         while (!jqDiv.@is("[data-feature-cmd]") && jqDiv.parent() != null)
            jqDiv = J(jqDiv.parent());

         // if this is true, we didn't find it
         if (!jqDiv.@is("[data-feature-cmd]"))
            return;

         JsString cmdType = jqDiv.attr("data-cmd-type");
         decimal currentValue = (decimal)parseFloat(jqDiv.attr("data-feature-value"));
         JsNumber featureID = parseInt(jqDiv.attr("data-feature-cmd"));
         //JsNumber featureID = parseInt(jqDiv.attr(((cmdType == "M") && (currentValue == 1)) ? "data-feature-off-cmd" : "data-feature-cmd"));
         decimal value = 0;

         switch (cmdType)
         {
            // momentary. We need to toggle on, then off
            //
            // Send the command with 1 ("on" value) passing the function to send the command again with 0 ("off" value)
            // as the success callback of the first command.
            case "M": 
               currentVehicle.Command(
                                      featureID, 1, 
                  null, null);
               window.setTimeout(() => currentVehicle.Command(featureID, 0, CommandSuccess, VehicleCommunicationError), 1000);
                                      //(arg1, arg2, arg3) =>
                                      //{
                                      //   if (arg2 == "parsererror") LockSuccessCallback(currentVehicle, featureID);
                                      //   else VehicleCommunicationError(arg1, arg2, arg3);
                                      //});
               break;
            case "B": // binary. We either set it on or off
               value = (currentValue == 0 ? 1 : 0);
               break;
            case "!B": // interpret the value as the inverse
               value = currentValue;
               break;
            case "A+": // analog increment. shows a number
               value = currentValue + 1;
               break;
            case "A-": // analog decrement. shows a number
               value = currentValue - 1;
               break;
            default: // what to do? bail
               return;
         }

         if (cmdType != "M")
            currentVehicle.Command(featureID, value, CommandSuccess, VehicleCommunicationError);
      }
   }

   private static JsAction<object, JsString, jqXHR> LockSuccessCallback(Vehicle currentVehicle, JsNumber featureID)
   {
      return (arg1, arg2, arg3) => window.setTimeout(() =>
         currentVehicle.Command(featureID, 0, CommandSuccess, VehicleCommunicationError), 1000);
   }

   //private static void SendMomentarySuccess(object currentVehicle, JsString featureID, jqXHR arg3)
   //{
   //   window.setTimeout(() => currentVehicle.As<Vehicle>().Command(featureID.As<JsNumber>(), 0, CommandSuccess, VehicleCommunicationError), 1000);
   //}

   private static void CommandSuccess(object arg1, JsString arg2, jqXHR arg3)
   {
      //connectionValidated = true;
      UpdateVehicleConnectionStatusDisplay(true);
   }

   private static void VehicleCommunicationError(jqXHR arg1, JsString arg2, JsError arg3)
   {
      if (arg2 != "parsererror") // this is thrown by calls to Command
      {
            //connectionValidated = false;
            UpdateVehicleConnectionStatusDisplay(false);
            Helper.LoadingDlg("hide");
            Helper.SlidePage("#tools", "L");
      }
   }

   private static void UpdateVehicleConnectionStatusDisplay(bool isConnected)
   {
      string status = (isConnected ? "C" : "Not c") + "onnected";
      JsString truckName = "";

      if (IsVehicleSelected)
      {
         truckName = ": " + CurrentConfig.CurrentVehicleData.ShortName;

      }
      else
      {
         status = "Select a vehicle";
      }

      J("#currentConnection").html(status + truckName);

      DimAllFeatures(!isConnected || !IsVehicleSelected);
      DimAllPM(!IsVehicleSelected);

      Dim(J("#logoutBtnWrapper"), !IsLoggedIn);
      Dim(J("#connectBtnWrapper"), isConnected || !IsVehicleSelected);
   }

   private static void ProcessStatus(object arg1, JsString arg2, jqXHR arg3)
   {
      /********************************************
       * 
       * data-feature-id = attribute of div surrounding the feature display elements. Indicates entire feature's UI
       * 
       * These three belong on the same DIV:
       *    data-feature-cmd = attribute of div surrounding buttons that send commands
       *    data-cmd-type = attribute of div surrounding buttons that send commands
       *    data-feature-value = attribute of div surrounding elements that have values (including buttons that send commands)
       * 
       * Note: buttons themselves have no data-feature attributes on them
       * 
       * ******************************************/

      //connectionValidated = true;
      UpdateVehicleConnectionStatusDisplay(true);

      FeatureStatus[] statuses = arg1.As<FeatureStatus[]>();

      for (int i = 0; i < statuses.Length; i++)
      {
         FeatureStatus status = statuses[i];
         jQuery jqIdDiv = J("[data-feature-id=" + status.id + "]");

         jqIdDiv.each(
                 (index, target) =>
                 {
                    jQuery jqCmdDiv = J(target);
                    if (jqCmdDiv.attr("data-feature-cmd") == null)
                       jqCmdDiv = J("[data-feature-cmd=" + status.id + "]", target);
                    JsString cmdType = jqCmdDiv.attr("data-cmd-type");
                    jQuery jqSpan = J(".ui-span-format", jqCmdDiv);

                    switch (cmdType)
                    {
                       case "T":
                       case "M":
                          if (status.value == 0)
                          {
                             jqCmdDiv.attr("data-feature-value", 0);
                             jqCmdDiv.removeClass("active");
                             jqSpan.html(jqSpan.attr("data-inactive-text"));
                          }
                          else
                          {
                             jqCmdDiv.attr("data-feature-value", 1);
                             jqCmdDiv.addClass("active");
                             jqSpan.html(jqSpan.attr("data-active-text"));
                          }
                          break;
                       case "B":
                          if (status.id != 420 && status.id != 159)  // suppress status updates for 420 (Interior Lights) and 159 (Exterior Lights)
                          {
                             if (status.value == 0)
                             {
                                jqCmdDiv.attr("data-feature-value", 0);
                                jqCmdDiv.removeClass("active");
                                //jqSpan.html("OFF");
                                jqSpan.html((status.id == 999) ? "START" : "OFF");
                             }
                             else
                             {
                                jqCmdDiv.attr("data-feature-value", 1);
                                jqCmdDiv.addClass("active");
                                //jqSpan.html("ON");
                                jqSpan.html((status.id == 999) ? "STOP" : "ON");
                             }
                             DimByStatusId(status.id.ToString(), (status.value == 0));
                          }
                          break;
                       case "!B":
                          if (status.value == 0)
                          {
                             jqCmdDiv.attr("data-feature-value", 1);
                             jqCmdDiv.addClass("active");
                             jqSpan.html("ON");
                             DimByStatusId(status.id.ToString(), true);
                          }
                          else
                          {
                             jqCmdDiv.attr("data-feature-value", 0);
                             jqCmdDiv.removeClass("active");
                             jqSpan.html("OFF");
                             DimByStatusId(status.id.ToString(), false);
                          }

                          break;

                       case "A+":
                       case "A-":
                          // these won't appear on buttons

                          jqCmdDiv.attr("data-feature-value", status.value);

                          // Find data-feature-display elements inside the data-feature-id div
                          // and update them with the status value converted to string

                          J("[data-feature-display]", J(target)).each(
                                                                  (displayIndex, displayTarget) =>
                                                                  J(displayTarget).html(status.value.As<JsString>()));
                          break;
                    }
                 });
      }
   }

   #endregion Send command to vehicle
}
