# Practical 9: Placement Dashboard using Data Controls & Crystal Reports

**Course:** 01CE1523 - .NET Technologies (CO4)  
**Student:** Basanagouda Sanjay Patil (92400120671), Batch B  
**Institution:** Faculty of Engineering and Technology, Marwadi University  

## Features
- Add placement records (`StudentName`, `Department`, `Company`, `PackageLPA`, `PlacementStatus`)
- Bound GridView-style Data Control table with delete support
- Search by student name or company and filter by department and placement status
- Dashboard KPIs: Total Students Placed, Placement Rate, Highest & Average Package
- Company-wise placement statistics summary table
- SQLite persistence via ADO.NET (`Microsoft.Data.Sqlite`)
- SAP Crystal Reports integration (`CrystalReports.Engine 13.0.4003`, `CrystalReports.Shared 13.0.4003`, `PlacementDataSet.xsd`, `ReportDocument`)

## Crystal Reports Runtime Compatibility Note
`CrystalReports.Engine` (v13.0.4003) targets `.NETFramework,Version=v4.0` and depends on `System.Windows.Forms` and the Windows-only SAP Crystal Reports Runtime (`crpe32.dll` COM engine). On macOS / .NET 7.0, the project compiles cleanly with the NuGet packages referenced, while invoking `new ReportDocument()` throws `TypeInitializationException` (`FileNotFoundException: System.Windows.Forms, Version=4.0.0.0`). On Windows with Visual Studio + SAP Crystal Reports Runtime installed, `PlacementReport.rpt` can be designed against `PlacementDataSet.xsd` and exported directly via `ReportDocument.ExportToDisk()`.

## Run Instructions
```bash
dotnet restore
dotnet build
dotnet run
```
