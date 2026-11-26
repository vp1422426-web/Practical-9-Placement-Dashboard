using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

namespace PlacementDashboardApp
{
    public class PlacementRecord
    {
        public int PlacementId { get; set; }
        public string StudentName { get; set; }
        public string Department { get; set; }
        public string Company { get; set; }
        public double PackageLPA { get; set; }
        public string PlacementStatus { get; set; }
    }

    public class CompanyStats
    {
        public string Company { get; set; }
        public int PlacedCount { get; set; }
        public double AvgPackage { get; set; }
        public double MaxPackage { get; set; }
    }

    public class PlacementDashboard
    {
        private static readonly string ConnectionString = "Data Source=placement.db";

        public static void Main(string[] args)
        {
            InitializeDatabase();
            SeedSamplePlacements();

            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();

            app.MapGet("/", (HttpContext ctx) =>
            {
                string search = ctx.Request.Query["search"].ToString().Trim();
                string dept = ctx.Request.Query["dept"].ToString().Trim();
                string status = ctx.Request.Query["status"].ToString().Trim();
                string msg = ctx.Request.Query["msg"].ToString();

                var records = GetPlacements(search, dept, status);
                var stats = GetCompanyStatistics();
                var summary = GetDashboardSummary();

                string html = RenderDashboardHtml(records, stats, summary, search, dept, status, msg);
                return Results.Content(html, "text/html; charset=utf-8");
            });

            app.MapPost("/add", async (HttpContext ctx) =>
            {
                var form = await ctx.Request.ReadFormAsync();
                string name = form["studentName"].ToString().Trim();
                string dept = form["department"].ToString().Trim();
                string company = form["company"].ToString().Trim();
                string status = form["status"].ToString().Trim();
                double packageLpa;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(company) ||
                    !double.TryParse(form["packageLpa"], out packageLpa) || packageLpa < 0)
                {
                    return Results.Redirect("/?msg=" + WebUtility.UrlEncode("Validation Error: Please provide valid student, company, and package details."));
                }

                AddPlacementRecord(name, dept, company, packageLpa, status);
                return Results.Redirect("/?msg=" + WebUtility.UrlEncode("Placement record added for " + name + "."));
            });

            app.MapPost("/delete/{id:int}", (int id) =>
            {
                DeletePlacementRecord(id);
                return Results.Redirect("/?msg=" + WebUtility.UrlEncode("Record ID " + id + " deleted."));
            });

            app.MapGet("/crystal-report", () =>
            {
                string result = TryExportCrystalReport();
                return Results.Redirect("/?msg=" + WebUtility.UrlEncode(result));
            });

            app.Run();
        }

        // ---------------- DATABASE OPERATIONS (ADO.NET) ----------------
        static void InitializeDatabase()
        {
            using (SqliteConnection conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                string sql = @"
                    CREATE TABLE IF NOT EXISTS Placements (
                        PlacementId INTEGER PRIMARY KEY AUTOINCREMENT,
                        StudentName TEXT NOT NULL,
                        Department TEXT NOT NULL,
                        Company TEXT NOT NULL,
                        PackageLPA REAL NOT NULL CHECK (PackageLPA >= 0),
                        PlacementStatus TEXT NOT NULL CHECK (PlacementStatus IN ('Placed', 'Shortlisted', 'Pending')),
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );";
                using (SqliteCommand cmd = new SqliteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        static void SeedSamplePlacements()
        {
            using (SqliteConnection conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                using (SqliteCommand countCmd = new SqliteCommand("SELECT COUNT(*) FROM Placements", conn))
                {
                    long count = (long)countCmd.ExecuteScalar();
                    if (count > 0) return;
                }

                var sample = new List<PlacementRecord>
                {
                    new PlacementRecord { StudentName = "Basanagouda Patil", Department = "CSE", Company = "TCS Digital", PackageLPA = 7.5, PlacementStatus = "Placed" },
                    new PlacementRecord { StudentName = "Aarav Sharma", Department = "CSE", Company = "Infosys SP", PackageLPA = 9.5, PlacementStatus = "Placed" },
                    new PlacementRecord { StudentName = "Priya Deshmukh", Department = "IT", Company = "TCS Digital", PackageLPA = 7.5, PlacementStatus = "Placed" },
                    new PlacementRecord { StudentName = "Rohan Kulkarni", Department = "CSE", Company = "Accenture", PackageLPA = 6.5, PlacementStatus = "Placed" },
                    new PlacementRecord { StudentName = "Sneha Joshi", Department = "AI & ML", Company = "Capgemini", PackageLPA = 5.75, PlacementStatus = "Shortlisted" },
                    new PlacementRecord { StudentName = "Karan Mehta", Department = "CE", Company = "Wipro", PackageLPA = 4.5, PlacementStatus = "Placed" },
                    new PlacementRecord { StudentName = "Neha Verma", Department = "IT", Company = "Infosys SP", PackageLPA = 9.5, PlacementStatus = "Placed" },
                    new PlacementRecord { StudentName = "Vivek Nair", Department = "CSE", Company = "Accenture", PackageLPA = 0.0, PlacementStatus = "Pending" }
                };

                foreach (var rec in sample)
                {
                    AddPlacementRecord(rec.StudentName, rec.Department, rec.Company, rec.PackageLPA, rec.PlacementStatus);
                }
            }
        }

        static void AddPlacementRecord(string studentName, string dept, string company, double packageLpa, string status)
        {
            using (SqliteConnection conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                string sql = @"INSERT INTO Placements (StudentName, Department, Company, PackageLPA, PlacementStatus)
                               VALUES (@StudentName, @Department, @Company, @PackageLPA, @Status)";
                using (SqliteCommand cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@StudentName", studentName);
                    cmd.Parameters.AddWithValue("@Department", string.IsNullOrWhiteSpace(dept) ? "CSE" : dept);
                    cmd.Parameters.AddWithValue("@Company", company);
                    cmd.Parameters.AddWithValue("@PackageLPA", packageLpa);
                    cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(status) ? "Pending" : status);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        static void DeletePlacementRecord(int id)
        {
            using (SqliteConnection conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM Placements WHERE PlacementId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        static List<PlacementRecord> GetPlacements(string search, string dept, string status)
        {
            var list = new List<PlacementRecord>();
            using (SqliteConnection conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                StringBuilder sql = new StringBuilder("SELECT PlacementId, StudentName, Department, Company, PackageLPA, PlacementStatus FROM Placements WHERE 1=1");
                using (SqliteCommand cmd = new SqliteCommand())
                {
                    cmd.Connection = conn;
                    if (!string.IsNullOrEmpty(search))
                    {
                        sql.Append(" AND (LOWER(StudentName) LIKE LOWER(@Search) OR LOWER(Company) LIKE LOWER(@Search))");
                        cmd.Parameters.AddWithValue("@Search", "%" + search + "%");
                    }
                    if (!string.IsNullOrEmpty(dept) && dept != "All")
                    {
                        sql.Append(" AND Department = @Dept");
                        cmd.Parameters.AddWithValue("@Dept", dept);
                    }
                    if (!string.IsNullOrEmpty(status) && status != "All")
                    {
                        sql.Append(" AND PlacementStatus = @Status");
                        cmd.Parameters.AddWithValue("@Status", status);
                    }
                    sql.Append(" ORDER BY PlacementId DESC");
                    cmd.CommandText = sql.ToString();

                    using (SqliteDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new PlacementRecord
                            {
                                PlacementId = r.GetInt32(0),
                                StudentName = r.GetString(1),
                                Department = r.GetString(2),
                                Company = r.GetString(3),
                                PackageLPA = r.GetDouble(4),
                                PlacementStatus = r.GetString(5)
                            });
                        }
                    }
                }
            }
            return list;
        }

        static List<CompanyStats> GetCompanyStatistics()
        {
            var stats = new List<CompanyStats>();
            using (SqliteConnection conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                string sql = @"
                    SELECT Company,
                           COUNT(*) AS PlacedCount,
                           AVG(PackageLPA) AS AvgPkg,
                           MAX(PackageLPA) AS MaxPkg
                    FROM Placements
                    WHERE PlacementStatus = 'Placed'
                    GROUP BY Company
                    ORDER BY PlacedCount DESC, MaxPkg DESC";

                using (SqliteCommand cmd = new SqliteCommand(sql, conn))
                using (SqliteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        stats.Add(new CompanyStats
                        {
                            Company = r.GetString(0),
                            PlacedCount = r.GetInt32(1),
                            AvgPackage = r.GetDouble(2),
                            MaxPackage = r.GetDouble(3)
                        });
                    }
                }
            }
            return stats;
        }

        static (int Total, int Placed, double Highest, double Average) GetDashboardSummary()
        {
            using (SqliteConnection conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                string sql = @"
                    SELECT COUNT(*),
                           SUM(CASE WHEN PlacementStatus = 'Placed' THEN 1 ELSE 0 END),
                           COALESCE(MAX(CASE WHEN PlacementStatus = 'Placed' THEN PackageLPA END), 0),
                           COALESCE(AVG(CASE WHEN PlacementStatus = 'Placed' THEN PackageLPA END), 0)
                    FROM Placements";
                using (SqliteCommand cmd = new SqliteCommand(sql, conn))
                using (SqliteDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        return (r.GetInt32(0), r.IsDBNull(1) ? 0 : r.GetInt32(1), r.GetDouble(2), r.GetDouble(3));
                    }
                }
            }
            return (0, 0, 0, 0);
        }

        // ---------------- SAP CRYSTAL REPORTS INTEGRATION ----------------
        static DataTable BuildPlacementDataTable()
        {
            DataTable dt = new DataTable("Placements");
            dt.Columns.Add("PlacementId", typeof(int));
            dt.Columns.Add("StudentName", typeof(string));
            dt.Columns.Add("Department", typeof(string));
            dt.Columns.Add("Company", typeof(string));
            dt.Columns.Add("PackageLPA", typeof(decimal));
            dt.Columns.Add("PlacementStatus", typeof(string));

            foreach (var rec in GetPlacements("", "", ""))
            {
                dt.Rows.Add(rec.PlacementId, rec.StudentName, rec.Department, rec.Company, (decimal)rec.PackageLPA, rec.PlacementStatus);
            }
            return dt;
        }

        static string TryExportCrystalReport()
        {
            string rptPath = Path.Combine(AppContext.BaseDirectory, "PlacementReport.rpt");
            if (!File.Exists(rptPath))
            {
                rptPath = Path.Combine(Directory.GetCurrentDirectory(), "PlacementReport.rpt");
            }

            try
            {
                if (!File.Exists(rptPath))
                {
                    return "Crystal Reports Notice: PlacementReport.rpt binary template must be authored with SAP Crystal Reports for Visual Studio (Windows IDE) using PlacementDataSet.xsd.";
                }

                DataTable dt = BuildPlacementDataTable();
                using (ReportDocument report = new ReportDocument())
                {
                    report.Load(rptPath);
                    report.SetDataSource(dt);
                    string outPdf = Path.Combine(Directory.GetCurrentDirectory(), "PlacementCrystalExport.pdf");
                    report.ExportToDisk(ExportFormatType.PortableDocFormat, outPdf);
                    return "Crystal Report exported successfully to " + outPdf;
                }
            }
            catch (TypeInitializationException ex)
            {
                string detail = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return "Crystal Reports Runtime Limitation (.NET Framework 4.x / Windows COM required): " + detail;
            }
            catch (Exception ex)
            {
                return "Crystal Reports Error: " + ex.Message;
            }
        }

        // ---------------- DATA CONTROLS DASHBOARD VIEW ----------------
        static string RenderDashboardHtml(
            List<PlacementRecord> records,
            List<CompanyStats> stats,
            (int Total, int Placed, double Highest, double Average) summary,
            string search,
            string dept,
            string status,
            string msg)
        {
            var sb = new StringBuilder();
            sb.Append(@"<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <title>Placement Dashboard - Data Controls &amp; Crystal Reports</title>
  <style>
    body { font-family: 'Segoe UI', Arial, sans-serif; background:#f4f6f9; color:#1f2937; margin:0; padding:18px 26px; }
    .header { display:flex; justify-content:space-between; align-items:center; border-bottom:2px solid #1e3a8a; padding-bottom:10px; margin-bottom:16px; }
    .header h1 { margin:0; font-size:21px; color:#1e3a8a; }
    .header small { color:#4b5563; font-size:12px; }
    .kpis { display:grid; grid-template-columns:repeat(4,1fr); gap:12px; margin-bottom:16px; }
    .kpi { background:#fff; border:1px solid #dbe2ea; border-left:4px solid #1e3a8a; border-radius:6px; padding:12px 14px; }
    .kpi .lbl { font-size:11px; text-transform:uppercase; color:#6b7280; font-weight:600; }
    .kpi .val { font-size:22px; font-weight:700; color:#111827; margin-top:4px; }
    .grid-2 { display:grid; grid-template-columns: 2.1fr 1fr; gap:16px; }
    .card { background:#fff; border:1px solid #dbe2ea; border-radius:6px; padding:14px; margin-bottom:14px; }
    .card h2 { margin:0 0 10px 0; font-size:15px; color:#1e3a8a; border-bottom:1px solid #e5e7eb; padding-bottom:6px; }
    .filters { display:flex; gap:8px; flex-wrap:wrap; margin-bottom:10px; align-items:center; }
    input, select { padding:6px 9px; border:1px solid #cbd5e1; border-radius:4px; font-size:13px; }
    button, .btn { background:#1e3a8a; color:#fff; border:none; padding:6px 12px; border-radius:4px; cursor:pointer; font-size:13px; text-decoration:none; display:inline-block; }
    .btn-cr { background:#0f766e; }
    .btn-del { background:#dc2626; padding:3px 8px; font-size:11px; }
    table { width:100%; border-collapse:collapse; font-size:12.5px; }
    th, td { border:1px solid #e2e8f0; padding:7px 9px; text-align:left; }
    th { background:#eff6ff; color:#1e3a8a; font-weight:600; }
    .badge { padding:2px 8px; border-radius:10px; font-size:11px; font-weight:600; }
    .Placed { background:#dcfce7; color:#166534; }
    .Shortlisted { background:#fef3c7; color:#92400e; }
    .Pending { background:#fee2e2; color:#991b1b; }
    .alert { background:#eff6ff; border:1px solid #93c5fd; color:#1e40af; padding:8px 12px; border-radius:5px; margin-bottom:12px; font-size:12.5px; }
    .cr-box { background:#f8fafc; border:1px dashed #64748b; padding:10px; border-radius:5px; font-size:12px; color:#334155; }
  </style>
</head>
<body>
  <div class='header'>
    <div>
      <h1>Placement Management Dashboard (Data Controls &amp; Crystal Reports)</h1>
      <small>01CE1523 - .NET Technologies | Basanagouda Sanjay Patil (92400120671) | Batch B</small>
    </div>
    <div>
      <a class='btn btn-cr' href='/crystal-report'>Run Crystal Report Export (.rpt)</a>
    </div>
  </div>");

            if (!string.IsNullOrEmpty(msg))
            {
                sb.Append("<div class='alert'>" + WebUtility.HtmlEncode(msg) + "</div>");
            }

            double rate = summary.Total > 0 ? (summary.Placed * 100.0 / summary.Total) : 0;
            sb.Append($@"
  <div class='kpis'>
    <div class='kpi'><div class='lbl'>Total Students Placed</div><div class='val'>{summary.Placed} / {summary.Total}</div></div>
    <div class='kpi'><div class='lbl'>Placement Rate</div><div class='val'>{rate:F1}%</div></div>
    <div class='kpi'><div class='lbl'>Highest Package</div><div class='val'>{summary.Highest:F2} LPA</div></div>
    <div class='kpi'><div class='lbl'>Average Package</div><div class='val'>{summary.Average:F2} LPA</div></div>
  </div>");

            sb.Append(@"
  <div class='grid-2'>
    <div>
      <div class='card'>
        <h2>Placement Records (Bound GridView Data Control)</h2>
        <form method='get' action='/' class='filters'>
          <input type='text' name='search' placeholder='Search student or company...' value='" + WebUtility.HtmlEncode(search) + @"' />
          <select name='dept'>
            <option value='All'" + (dept == "All" || dept == "" ? " selected" : "") + @">All Departments</option>
            <option value='CSE'" + (dept == "CSE" ? " selected" : "") + @">CSE</option>
            <option value='IT'" + (dept == "IT" ? " selected" : "") + @">IT</option>
            <option value='AI &amp; ML'" + (dept == "AI & ML" ? " selected" : "") + @">AI &amp; ML</option>
            <option value='CE'" + (dept == "CE" ? " selected" : "") + @">CE</option>
          </select>
          <select name='status'>
            <option value='All'" + (status == "All" || status == "" ? " selected" : "") + @">All Status</option>
            <option value='Placed'" + (status == "Placed" ? " selected" : "") + @">Placed</option>
            <option value='Shortlisted'" + (status == "Shortlisted" ? " selected" : "") + @">Shortlisted</option>
            <option value='Pending'" + (status == "Pending" ? " selected" : "") + @">Pending</option>
          </select>
          <button type='submit'>Filter</button>
          <a href='/' style='font-size:12px;color:#1e3a8a;'>Reset</a>
        </form>
        <table>
          <thead><tr><th>ID</th><th>Student Name</th><th>Dept</th><th>Company</th><th>Package</th><th>Status</th><th>Action</th></tr></thead>
          <tbody>");

            foreach (var r in records)
            {
                sb.Append($@"<tr>
                  <td>{r.PlacementId}</td>
                  <td>{WebUtility.HtmlEncode(r.StudentName)}</td>
                  <td>{WebUtility.HtmlEncode(r.Department)}</td>
                  <td>{WebUtility.HtmlEncode(r.Company)}</td>
                  <td>{r.PackageLPA:F2} LPA</td>
                  <td><span class='badge {r.PlacementStatus}'>{WebUtility.HtmlEncode(r.PlacementStatus)}</span></td>
                  <td><form method='post' action='/delete/{r.PlacementId}' style='margin:0;'><button class='btn-del' type='submit'>Delete</button></form></td>
                </tr>");
            }

            sb.Append(@"
          </tbody>
        </table>
      </div>
    </div>
    <div>
      <div class='card'>
        <h2>Company-Wise Placement Statistics</h2>
        <table>
          <thead><tr><th>Company</th><th>Placed</th><th>Avg LPA</th><th>Max LPA</th></tr></thead>
          <tbody>");

            foreach (var s in stats)
            {
                sb.Append($"<tr><td>{WebUtility.HtmlEncode(s.Company)}</td><td>{s.PlacedCount}</td><td>{s.AvgPackage:F2}</td><td>{s.MaxPackage:F2}</td></tr>");
            }

            sb.Append(@"
          </tbody>
        </table>
      </div>
      <div class='card'>
        <h2>Add Placement Record</h2>
        <form method='post' action='/add' style='display:flex;flex-direction:column;gap:8px;'>
          <input type='text' name='studentName' placeholder='Student Name' required />
          <select name='department'>
            <option value='CSE'>CSE</option>
            <option value='IT'>IT</option>
            <option value='AI &amp; ML'>AI &amp; ML</option>
            <option value='CE'>CE</option>
          </select>
          <input type='text' name='company' placeholder='Company Name' required />
          <input type='number' step='0.01' name='packageLpa' placeholder='Package (LPA)' required />
          <select name='status'>
            <option value='Placed'>Placed</option>
            <option value='Shortlisted'>Shortlisted</option>
            <option value='Pending'>Pending</option>
          </select>
          <button type='submit'>Add Record</button>
        </form>
      </div>
      <div class='card'>
        <h2>SAP Crystal Reports Engine</h2>
        <div class='cr-box'>
          <b>NuGet Packages:</b> CrystalReports.Engine 13.0.4003, CrystalReports.Shared 13.0.4003<br/>
          <b>Typed Schema:</b> PlacementDataSet.xsd<br/>
          <b>Report Engine:</b> CrystalDecisions.CrystalReports.Engine.ReportDocument<br/>
          <b>Environment Note:</b> Native execution of .rpt requires Windows .NET Framework 4.x &amp; SAP CRRuntime (crpe32.dll).
        </div>
      </div>
    </div>
  </div>
</body>
</html>");
            return sb.ToString();
        }
    }
}
