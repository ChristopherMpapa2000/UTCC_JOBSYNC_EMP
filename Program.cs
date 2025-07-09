using Jobsync.model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jobsync
{
    class Program
    {
        public static string _Connection = ConfigurationSettings.AppSettings["ConnectionString"];
        public static string _LogFile = ConfigurationSettings.AppSettings["LogFile"];
        public static string _Url = ConfigurationSettings.AppSettings["Url"];
        public static string _BearerToken = ConfigurationSettings.AppSettings["BearerToken"];
        public static string _Fixyear = ConfigurationSettings.AppSettings["Fixyear"];
        public static string _FixyearAgo = ConfigurationSettings.AppSettings["FixyearAgo"];
        public static void Log(String iText)
        {
            string pathlog = _LogFile;
            String logFolderPath = System.IO.Path.Combine(pathlog, DateTime.Now.ToString("yyyyMMdd"));

            if (!System.IO.Directory.Exists(logFolderPath))
            {
                System.IO.Directory.CreateDirectory(logFolderPath);
            }
            String logFilePath = System.IO.Path.Combine(logFolderPath, DateTime.Now.ToString("yyyyMMdd") + ".txt");

            try
            {
                using (System.IO.StreamWriter outfile = new System.IO.StreamWriter(logFilePath, true))
                {
                    System.Text.StringBuilder sbLog = new System.Text.StringBuilder();

                    String[] listText = iText.Split('|').ToArray();

                    foreach (String s in listText)
                    {
                        sbLog.AppendLine($"[{DateTime.Now:HH:mm:ss}] {s}");
                    }

                    outfile.WriteLine(sbLog.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing log file: {ex.Message}");
            }
        }
        static async Task Main(string[] args)
        {
            try
            {
                Log("====== Start Process ====== : " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                Log(string.Format("Run batch as :{0}", System.Security.Principal.WindowsIdentity.GetCurrent().Name));
                DataconDataContext db = new DataconDataContext(_Connection);
                if (db.Connection.State == ConnectionState.Open)
                {
                    db.Connection.Close();
                    db.Connection.Open();
                }
                db.Connection.Open();
                db.CommandTimeout = 0;

                Getdata(db);

            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR");
                Console.WriteLine("Exit ERROR");
                Log("ERROR");
                Log("message: " + ex.Message);
                Log("Exit ERROR");
            }
            finally
            {
                Log("====== End Process Process ====== : " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            }
        }
        public static void Getdata(DataconDataContext db)
        {
            using (HttpClient client = new HttpClient())
            {
                string year = string.Empty;
                List<string> lstyear = new List<string>();
                if (!string.IsNullOrEmpty(_Fixyear))
                {
                    year = _Fixyear;
                }
                else if (!string.IsNullOrEmpty(_FixyearAgo))
                {
                    int fixYearAgo = 0;
                    if (int.TryParse(_FixyearAgo, out fixYearAgo))
                    {
                        for (int i = 0; i <= Math.Abs(fixYearAgo); i++)
                        {
                            var dt = DateTime.Now.AddYears(-i);
                            lstyear.Add(dt.ToString("yyyy", new CultureInfo("th-TH")));
                        }
                    }
                }
                else
                {
                    year = DateTime.Now.ToString("yyyy", new CultureInfo("th-TH"));
                }
                try
                {
                    if (lstyear.Count > 0)
                    {
                        lstyear = lstyear.OrderBy(x => int.Parse(x)).ToList();
                        foreach (var yy in lstyear)
                        {
                            var url = _Url + yy;
                            var token = _BearerToken;
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                            var content = new StringContent("{}", Encoding.UTF8, "application/json");
                            var response = client.PostAsync(url, content).GetAwaiter().GetResult();
                            var result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            var Resultapi = JsonConvert.DeserializeObject<List<DataStudent>>(result);
                            Console.WriteLine("Student Count : " + Resultapi.Count());
                            Log("Student Count : " + Resultapi.Count());
                            if (Resultapi.Count > 0)
                            {
                                SyncEmployee(db, Resultapi);
                            }
                        }
                    }
                    else
                    {
                        var url = _Url + year;
                        var token = _BearerToken;
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        var content = new StringContent("{}", Encoding.UTF8, "application/json");
                        var response = client.PostAsync(url, content).GetAwaiter().GetResult();
                        var result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        var Resultapi = JsonConvert.DeserializeObject<List<DataStudent>>(result);
                        Console.WriteLine("Student Count : " + Resultapi.Count());
                        Log("Student Count : " + Resultapi.Count());
                        if (Resultapi.Count > 0)
                        {
                            SyncEmployee(db, Resultapi);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error Getdata: " + ex);
                    Log("Error Getdata : " + ex);
                }
            }
        }
        public static void SyncEmployee(DataconDataContext db, List<DataStudent> Student)
        {
            foreach (var item in Student)
            {
                if (!string.IsNullOrEmpty(item.STUDENTCODE))
                {
                    #region Set Data
                    string NameTh = string.Join(" ", new[] { item.PREFIXNAME, item.STUDENTNAME, item.STUDENTSURNAME }.Where(s => !string.IsNullOrEmpty(s)));
                    string NameEn = string.Join(" ", new[] { item.PREFIXNAMEENG, item.STUDENTNAMEENG, item.STUDENTSURNAMEENG }.Where(s => !string.IsNullOrEmpty(s)));
                    bool Active = false;
                    if (item.STUDENTSTATUS < 40)
                    {
                        Active = true;
                    }
                    else
                    {
                        Log("Skip STUDENTSTATUS >= 40 : " + item.STUDENTCODE);
                        continue;
                    }
                    var empreport_to = string.Empty;
                    if (item.ADEMAIL != null)
                    {
                        var ReportTo = db.MSTEmployees.Where(x => x.Email == item.ADEMAIL.Trim()).FirstOrDefault();
                        if (ReportTo != null)
                        {
                            empreport_to = ReportTo.EmployeeId.ToString();
                        }
                    }
                    #endregion

                    var checkempcode = db.MSTEmployees.Where(x => x.EmployeeCode == item.STUDENTCODE.Trim()).FirstOrDefault();
                    if (checkempcode != null)
                    {
                        #region Update Data
                        var stdemp = checkempcode;
                        stdemp.EmployeeCode = item.STUDENTCODE;
                        stdemp.Username = item.STUDENTEMAIL;
                        stdemp.NameTh = NameTh;
                        stdemp.NameEn = NameEn;
                        stdemp.Email = item.STUDENTEMAIL;
                        stdemp.IsActive = Active;
                        stdemp.PositionId = 181;
                        stdemp.DepartmentId = 61;
                        stdemp.DivisionId = 31;
                        stdemp.ReportToEmpCode = empreport_to;
                        stdemp.Lang = "TH";
                        stdemp.AccountId = 1;
                        stdemp.ModifiedDate = DateTime.Now;
                        stdemp.ModifiedBy = "admin";
                        db.SubmitChanges();
                        Log("======= Update =======");
                        Log("STUDENTCODE : " + item.STUDENTCODE);
                        Log("NameTh : " + NameTh);
                        Log("NameEn : " + NameEn);
                        Log("Email : " + item.STUDENTEMAIL);
                        Log("IsActive : " + Active);
                        Log("PositionId : 181");
                        Log("DepartmentId : 61");
                        Log("DivisionId : 31");
                        Log("ReportToEmpCode : " + empreport_to);
                        Log("======================");
                        #endregion
                    }
                    else
                    {
                        #region Insert Data
                        MSTEmployee emp = new MSTEmployee();
                        emp.EmployeeCode = item.STUDENTCODE;
                        emp.Username = item.STUDENTEMAIL;
                        emp.NameTh = NameTh;
                        emp.NameEn = NameEn;
                        emp.Email = item.STUDENTEMAIL;
                        emp.IsActive = Active;
                        emp.PositionId = 181;
                        emp.DepartmentId = 61;
                        emp.DivisionId = 31;
                        emp.ReportToEmpCode = empreport_to;
                        emp.Lang = "TH";
                        emp.AccountId = 1;
                        emp.CreatedDate = DateTime.Now;
                        emp.CreatedBy = "admin";
                        emp.ModifiedDate = DateTime.Now;
                        emp.ModifiedBy = "admin";
                        db.MSTEmployees.InsertOnSubmit(emp);
                        db.SubmitChanges();
                        Log("======= Insert =======");
                        Log("STUDENTCODE : " + item.STUDENTCODE);
                        Log("NameTh : " + NameTh);
                        Log("NameEn : " + NameEn);
                        Log("Email : " + item.STUDENTEMAIL);
                        Log("IsActive : " + Active);
                        Log("PositionId : 181");
                        Log("DepartmentId : 61");
                        Log("DivisionId : 31");
                        Log("ReportToEmpCode : " + empreport_to);
                        Log("======================");
                        #endregion
                    }
                }
                else
                {
                    Log("Not have STUDENTCODE : " + item.STUDENTEMAIL);
                }
            }
        }
    }
}
