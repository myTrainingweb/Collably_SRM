using IP.ActionFilters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlusCP.Models;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace PlusCP.Controllers
{
    [OutputCache(Duration = 0)]
    [SessionTimeout]
    public class NewPOController : Controller
    {
        // GET: PO
        NewPO oPO = new NewPO();
        public ActionResult Option()
        {
            oPO = new NewPO();
            return View(oPO);
        }

        public ActionResult Index(string RptCode, string menuTitle)
        {
            oPO = new NewPO();
            TempData["ReportTitle"] = menuTitle;
            TempData["RptCode"] = RptCode;
            string Status = Request.QueryString["POStatus"];
            ViewBag.ReportTitle = "Purchase Order" + " > " + Status;
            return View(oPO);

        }

        public async Task<JsonResult> GetListAPI(string POStatus)
        {
            DataTable dt = new DataTable();
            string menuTitle = string.Empty;
            string RptCode;

            var client = new RestClient("https://centralusdtapp33.epicorsaas.com");
            var request = new RestRequest("/saas838/api/v1/BaqSvc/OpenPO_BuyerSupplier", Method.Get);
            // Add basic authentication header
            request.AddHeader("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes("ApiUser:Ap1User1234")));

            var response = client.Execute(request);

            if (response.IsSuccessStatusCode == true)
            {
                string jsonstring = response.Content;
                dt = Tabulate(jsonstring);

                oPO.GetList(dt, POStatus);

                var jsonResult = Json(oPO, JsonRequestBehavior.AllowGet);
                jsonResult.MaxJsonLength = int.MaxValue;
                //LOAD MRU & LOG QUERY
                if (TempData["ReportTitle"] != null && TempData["RptCode"] != null)
                {
                    menuTitle = TempData["ReportTitle"] as string;
                    RptCode = TempData["RptCode"].ToString();
                    TempData.Keep();
                    //cLog oLog = new cLog();
                    //oLog.SaveLog(menuTitle, Request.Url.PathAndQuery, RptCode);
                }
                return jsonResult;
            }
            else
            {
                return Json("");
            }
        }

        public async Task<JsonResult> GetList(string POStatus)
        {
            DataTable dt = new DataTable();
            string menuTitle = string.Empty;
            string RptCode;
            dt = GetPOData();
            oPO.GetList(dt, POStatus);
            var jsonResult = Json(oPO, JsonRequestBehavior.AllowGet);
            jsonResult.MaxJsonLength = int.MaxValue;
            //LOAD MRU & LOG QUERY
            if (TempData["ReportTitle"] != null && TempData["RptCode"] != null)
            {
                menuTitle = TempData["ReportTitle"] as string;
                RptCode = TempData["RptCode"].ToString();
                TempData.Keep();
            }
            return jsonResult;
        }

        public JsonResult SendEmail(string PONo, string Line, string Release, string PartNo, string PartDesc, string UOM,
            string OrderDate, string DueDate, string Qty, string Price, string VendorId, string VendorName,
           string BuyerId, string Receiveremail, string contactReason, string message)
        {


            string userName = Session["Username"].ToString();
            // Create New GUID
            Guid newGuid = Guid.NewGuid();

            // Insert Into BuyerPO table
            oPO.AddInBuyers(PONo, Line, Release, PartNo, PartDesc, Qty, Price, UOM, DueDate, OrderDate, userName,
               VendorId, VendorName, Receiveremail, BuyerId, contactReason, userName, newGuid.ToString());

            // Insert Into BuyerCommunication table and sent email
            oPO.sendVendorEmail(userName, PONo, PartNo, Qty, DueDate, Price, message, newGuid.ToString());



            var jsonResult = Json("Send", JsonRequestBehavior.AllowGet);
            jsonResult.MaxJsonLength = int.MaxValue;


            return jsonResult;
        }

        public static DataTable Tabulate(string json)
        {
            var jsonLinq = JObject.Parse(json);
            int counter = 0;
            //int counter = 0;
            // Find the first array using Linq
            var srcArray = jsonLinq.Descendants().Where(d => d is JArray).First();
            var trgArray = new JArray();
            foreach (JObject row in srcArray.Children<JObject>())
            {
                var cleanRow = new JObject();
                foreach (JProperty column in row.Properties())
                {
                    // Only include JValue types
                    if (column.Value is JValue)
                    {
                        cleanRow.Add(column.Name, column.Value);
                    }
                }
                trgArray.Add(cleanRow);
                //counter = counter + 1;

                //if (counter < 100)
                //    trgArray.Add(cleanRow);
                //else
                //    break;

            }

            return JsonConvert.DeserializeObject<DataTable>(trgArray.ToString());
        }

        public static DataTable ConvertJsonToDataTable(string json)
        {
            // Parse the JSON
            JToken token = JToken.Parse(json);

            // Initialize DataTable
            DataTable dataTable = new DataTable();

            if (token.Type == JTokenType.Array)
            {
                // If JSON is an array, extract column names from the properties of the first object
                JArray jsonArray = (JArray)token;
                if (jsonArray.Count > 0)
                {
                    JObject firstObject = (JObject)jsonArray.First;
                    foreach (JProperty property in firstObject.Properties())
                    {
                        dataTable.Columns.Add(property.Name, typeof(string)); // Adjust type as needed
                    }

                    // Populate the DataTable with JSON data
                    var rows = jsonArray.Select(j => ((JObject)j).Properties()
                                        .Select(p => p.Value.ToString()).ToArray());
                    foreach (var row in rows)
                    {
                        dataTable.Rows.Add(row);
                    }
                }
            }
            else if (token.Type == JTokenType.Object)
            {
                // If JSON is a single object, extract column names from its properties
                JObject jsonObject = (JObject)token;
                foreach (JProperty property in jsonObject.Properties())
                {
                    dataTable.Columns.Add(property.Name, typeof(string)); // Adjust type as needed
                }

                // Populate a single row in the DataTable
                dataTable.Rows.Add(jsonObject.Properties()
                                        .Select(p => p.Value.ToString()).ToArray());
            }
            else
            {
                throw new ArgumentException("Invalid JSON format.");
            }

            return dataTable;
        }

        public ActionResult SupplierUpdate(string RptCode, string menuTitle)
        {
            oPO = new NewPO();
            TempData["ReportTitle"] = menuTitle;
            TempData["RptCode"] = RptCode;
            ViewBag.ReportTitle = "Update from Supplier";
            return View(oPO);

        }

        public JsonResult GetUpdateData()
        {
            string menuTitle = string.Empty;
            string RptCode;

            oPO = new NewPO();
            oPO.GetUpdateData("");
            var jsonResult = Json(oPO, JsonRequestBehavior.AllowGet);
            jsonResult.MaxJsonLength = int.MaxValue;
            //LOAD MRU & LOG QUERY
            if (TempData["ReportTitle"] != null && TempData["RptCode"] != null)
            {
                menuTitle = TempData["ReportTitle"] as string;
                RptCode = TempData["RptCode"].ToString();
                TempData.Keep();
                cLog oLog = new cLog();
                oLog.SaveLog(menuTitle, Request.Url.PathAndQuery, RptCode);
            }
            return jsonResult;
        }

        public ActionResult GetUpdateNew(string PONo)
        {
            try
            {
                ViewBag.ReportTitle = "PO @ " + PONo + "";
                NewPO oPO = new NewPO();
                bool success = oPO.GetUpdateData(PONo);
                oPO.serializer = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };

                if (success)
                    return View(oPO);
                else
                    return View();
            }
            catch (Exception e)
            {
                ViewBag.ErrMessage = e.Message;
                return View();
            }
        }

        public JsonResult UpdateHasAction(string ActionAHR, string GUID)
        {

            oPO = new NewPO();
            oPO.UpdateHasActionAHR(ActionAHR, GUID);

            var jsonResult = Json("Updated", JsonRequestBehavior.AllowGet);
            jsonResult.MaxJsonLength = int.MaxValue;


            return jsonResult;
        }

        public JsonResult ProposeUpdate(string GUID, string Qty, string Price, string DueDate, string Message, string PONo, string PartNo)
        {
            string userName = Session["Username"].ToString();
            oPO = new NewPO();
            oPO.UpdatePropose(GUID, Qty, Price, DueDate, Message, userName, PONo, PartNo);

            var jsonResult = Json("Updated", JsonRequestBehavior.AllowGet);
            jsonResult.MaxJsonLength = int.MaxValue;


            return jsonResult;
        }

        public DataTable GetPOData()
        {
            cDAL oDAL = new cDAL(cDAL.ConnectionType.ACTIVE);
            string sql = @"select * from [dbo].[tblPurchaseOrder]";
            DataTable dt = new DataTable();
            dt = oDAL.GetData(sql);
            return dt;
        }
    }
}