using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace SmartOps.Controllers
{
    public class DiagnosticsController : Controller
    {
        public ActionResult Index()
        {
            // Update this string or pull from Web.config
            string connectionString = "Server=(localdb)\\mssqllocaldb;Database=SmartOps;Trusted_Connection=True;MultipleActiveResultSets=true";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    ViewBag.Status = "Success";
                    ViewBag.Message = "Successfully connected to: " + conn.DataSource;
                    ViewBag.Version = conn.ServerVersion;
                }
            }
            catch (Exception ex)
            {
                ViewBag.Status = "Error";
                ViewBag.Message = ex.Message;
            }

            return View();
        }
    }
}
