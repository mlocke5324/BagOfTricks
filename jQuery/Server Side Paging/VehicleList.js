$(function () {
   // Constants for readability & consistency
   var classRowSelected = "js_RowSelected";
   var classTextAlignCenter = "text_center";

   // Selectors of specific items/controls
   var selectorButtonRequestData = "#btnRequestPmData";
   var selectorCheckboxSelectAll = "#cbxPmDataUpdate";
   var selectorTable = "#tblVehicle";

   // Selectors of categories of items/controls
   var selectorCheckbox = "input[type='checkbox']";
   var selectorHideShowControls = ".js_HideShow";
   var selectorTableBodyRows = selectorTable + " tbody tr";
   var selectorTableBodyRowsSelected = selectorTableBodyRows + "." + classRowSelected;
   var selectorTableBodyRowCheckboxes = selectorTableBodyRows + " " + selectorCheckbox;

   // Variables
   var vehicleDatatable;         // Reference to initialized vehicle datatable
   var bFlushCache;              // True - Host should flush vehicle PM Report cache; False - Host will not flush cache

   // Enable/Disable controls depending on presence/state of checkboxes
   function setControlStates() {
      var checkboxCount = $(selectorTableBodyRowCheckboxes).length;
      var selectedRowCount = $(selectorTableBodyRowsSelected).length;

      $(selectorCheckboxSelectAll).prop("checked", ((checkboxCount > 0) & (checkboxCount == selectedRowCount)));
      $(selectorButtonRequestData).prop("disabled", selectedRowCount == 0);

      if (checkboxCount == 0) {
         $(selectorHideShowControls).addClass("css_hidden");
      } else {
         $(selectorHideShowControls).removeClass("css_hidden");
      }
   }
   
   // Is a checkbox checked?
   function checkboxIsChecked(checkbox) {
      if (checkbox.length == 0)
         return false;

      if (checkbox.attr("type") != "checkbox")
         return false;

      return checkbox.attr("checked") == "checked";
   }

   // Set jQuery Datatable properties for the vehicle display table
   vehicleDatatable = $(selectorTable).dataTable(
   {
      "bServerSide": true,
      "sAjaxSource": "/API/Vehicle/GetVehicleTableData",
      "bProcessing": true,
      "sServerMethod": "POST",
      "iDisplayLength": 25,
      "bStateSave": false,
      "fnServerParams": function (aoData) {
         aoData.push({ "name": "agencyId", "value": $("#Agency_Id").val() });
         aoData.push({ "name": "bFlushCache", "value": bFlushCache ? "true" : "false" });
         var ids = new Array();
         $(selectorTableBodyRowsSelected).each(function (index, elementValue) {
            ids.push(vehicleDatatable.fnGetData(elementValue)[8]);
         });
         for (var i = 0; i < ids.length ; i++) {
            aoData.push({ "name": "vehicleIds", "value": ids[i] });
         }

         $(selectorButtonRequestData).prop("disabled", true);           //... disable Request PM Data button
         $(selectorCheckboxSelectAll).prop("checked", false);           // clear any check in the request
      },
      "fnRowCallback": function (nRow, aData, iDisplayIndex, iDisplayIndexFull) {
         // Set click action for checkbox on each vehicle row
         var checkbox = $(selectorCheckbox, nRow);
         if (checkbox.length > 0) {
            checkbox.click(function () {
               // Add/remove class so we can find the rows with checked checkboxes
               if (checkboxIsChecked($(this)))
                  $(this).parents("tr").addClass(classRowSelected);
               else
                  $(this).parents("tr").removeClass(classRowSelected);

               setControlStates();
            });
            if (checkboxIsChecked(checkbox))
               $(nRow).addClass(classRowSelected);
         }

         if (aData[5] == "--") {       // If PM data date had no value, we were sent "--" - it needs to be centered
            $("td:eq(5)", nRow).addClass(classTextAlignCenter);
         }
      },
      "fnDrawCallback": function (oSettings) {
         //alert('DataTables has redrawn the table');
         setControlStates();
         bFlushCache = false;
      },
      "oLanguage":
          {
             "sSearch": "Find"           // set label of full table search input field
          },
      "bJQueryUI": true,                          // Apply JQuery UI classes
      "sPaginationType": "full_numbers",          // Show page numbers instead of << >> in pagination control strip
      "bAutoWidth": false,
      "aoColumnDefs": // Column defintions
      [
          { "aTargets": [0], "sWidth": "5.5em", "bSortable": false, "bSearchable": false }, // Edit links/icons
          { "aTargets": [4], "sWidth": "7em" }, // Active Checkbox
          { "aTargets": [5], "sType": "date" }, // Last PM data date
          { "aTargets": [6], "sWidth": "11em", "bSortable": false, "bSearchable": false, "sClass": "text_right" }, // PM Data checkbox
          { "aTargets": [7], "sWidth": "12em", "bSortable": false, "bSearchable": false }, // PM Data update status
          { "aTargets": [8], "bVisible": false } // Vehicle Id - hidden column
      ]
   });

   // Set action for Request PM Data button
   $(selectorButtonRequestData).click(function () {
      bFlushCache = true;                 // Must set before call to fnDraw
      vehicleDatatable.fnDraw();
   });

   // Set action for PM Data Update checkbox - can't set actions for vehicle row checkboxes
   // because they don't exist until the first datatable ajax call (they are set there).
   $(selectorCheckboxSelectAll).click(function () {
      var check = checkboxIsChecked($(this));
      var checkboxes = $(selectorTableBodyRowCheckboxes);
      if (checkboxes.length > 0) {
         checkboxes.prop("checked", check); // Check/uncheck all row checkboxes

         // Add/remove class so we can find the rows with checked checkboxes when the Request PM Data button is clicked
         if (check)
            checkboxes.parents("tr").addClass(classRowSelected);
         else
            checkboxes.parents("tr").removeClass(classRowSelected);

         setControlStates();
      }

   });

   // At page ready event...
   setControlStates();
   bFlushCache = false;


});
