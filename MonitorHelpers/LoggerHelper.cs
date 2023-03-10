using Microsoft.Extensions.Configuration;
using Dapper;
using Npgsql;

namespace MDR_Importer;

public class LoggingHelper : ILoggingHelper
{
    private readonly string? _logfileStartOfPath;
    private readonly string? _summaryLogfileStartOfPath;  
    private string _logfilePath = "";
    private string _summaryLogfilePath = "";
    private StreamWriter? _sw;

    public LoggingHelper()
    {
        IConfigurationRoot settings = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        _logfileStartOfPath = settings["logFilePath"] ?? "";
        _summaryLogfileStartOfPath = settings["summaryFilePath"] ?? "";
    }

    public string LogFilePath => _logfilePath;

    
    public void OpenLogFile(string databaseName)
    {
        string dtString = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
            .Replace(":", "").Replace("T", " ");

        string logFolderPath = Path.Combine(_logfileStartOfPath!, databaseName);
        if (!Directory.Exists(logFolderPath))
        {
            Directory.CreateDirectory(logFolderPath);
        }
        
        string logFileName = "IM " + databaseName + " " + dtString + ".log";
        _logfilePath = Path.Combine(logFolderPath, logFileName);
        _summaryLogfilePath = Path.Combine(_summaryLogfileStartOfPath!, logFileName);
        _sw = new StreamWriter(_logfilePath, true, System.Text.Encoding.UTF8);
    }
    

    public void OpenNoSourceLogFile()
    {
        string dtString = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
            .Replace(":", "").Replace("T", " ");
        
        string logFileName = "IM Source not set " + dtString + ".log";
        _logfilePath = Path.Combine(_logfileStartOfPath!, logFileName);
        _summaryLogfilePath = Path.Combine(_summaryLogfileStartOfPath!, logFileName);
        _sw = new StreamWriter(_logfilePath, true, System.Text.Encoding.UTF8);
    }

    public void LogLine(string message, string identifier = "")
    {
        string dtPrefix = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
        string feedback = dtPrefix + message + identifier;
        Transmit(feedback);
    }


    public void LogHeader(string message)
    {
        string dtPrefix = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
        string header = dtPrefix + "**** " + message.ToUpper() + " ****";
        Transmit("");
        Transmit(header);
    }


    public void LogStudyHeader(Options opts, string dbLine)
    {
        string dividerLine = opts.UsingTestData ? new string('-', 70) : new string('=', 70);
        LogLine("");
        LogLine(dividerLine);
        LogLine(dbLine);
        LogLine(dividerLine);
        LogLine("");
    }


    public void LogError(string message)
    {
        string dtPrefix = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
        string errorMessage = dtPrefix + "***ERROR*** " + message;
        Transmit("");
        Transmit("+++++++++++++++++++++++++++++++++++++++");
        Transmit(errorMessage);
        Transmit("+++++++++++++++++++++++++++++++++++++++");
        Transmit("");
    }


    public void LogCodeError(string header, string errorMessage, string? stackTrace)
    {
        string dtPrefix = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
        string headerMessage = dtPrefix + "***ERROR*** " + header + "\n";
        Transmit("");
        Transmit("+++++++++++++++++++++++++++++++++++++++");
        Transmit(headerMessage);
        Transmit(errorMessage + "\n");
        Transmit(stackTrace ?? "");
        Transmit("+++++++++++++++++++++++++++++++++++++++");
        Transmit("");
    }


    public void LogParseError(string header, string errorNum, string errorType)
    {
        string dtPrefix = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
        string errorMessage = dtPrefix + "***ERROR*** " + "Error " + errorNum + ": " + header + " " + errorType;
        Transmit(errorMessage);
    }
            

    public void LogCommandLineParameters(Options opts)
    {
        int[]? sourceIds = opts.SourceIds?.ToArray();
        if (sourceIds?.Length > 0)
        {
            if (sourceIds.Length == 1)
            {
                LogLine("Source_id is " + sourceIds[0]);
            }
            else
            {
                LogLine("Source_ids are " + string.Join(",", sourceIds));
            }
        }

        LogLine("Rebuild AD tables is: " + opts.RebuildAdTables);
        LogLine("Using test data is: " + opts.UsingTestData);
        LogLine("Creating test report is: " + opts.CreateTestReport);
        LogLine("");
    }


    public void CloseLog()
    {
        if (_sw is not null)
        {
            LogHeader("Closing Log");
            _sw.Flush();
            _sw.Close();
        }
        
        // Write out the summary file.
        
        var swSummary = new StreamWriter(_summaryLogfilePath, true, System.Text.Encoding.UTF8);
        
        swSummary.Flush();
        swSummary.Close();
    }


    public void LogTableStatistics(Source s, string schema)
    {
        // Gets and logs record count for each table in the ad schema of the database
        // Start by obtaining connection string, then construct log line for each by 
        // calling db interrogation for each applicable table.
        
        string dbConn = s.db_conn ?? "";

        LogLine("");
        LogLine("TABLE RECORD NUMBERS");

        if (s.has_study_tables is true)
        {
            LogLine("");
            LogLine("study tables...\n");
            LogLine(GetTableRecordCount(dbConn, schema, "studies"));
            LogLine(GetTableRecordCount(dbConn, schema, "study_identifiers"));
            LogLine(GetTableRecordCount(dbConn, schema, "study_titles"));

            // these are database dependent
            if (s.has_study_topics is true) LogLine(GetTableRecordCount(dbConn, schema, "study_topics"));
            if (s.has_study_features is true) LogLine(GetTableRecordCount(dbConn, schema, "study_features"));
            if (s.has_study_people is true) LogLine(GetTableRecordCount(dbConn, schema, "study_people"));
            if (s.has_study_organisations is true) LogLine(GetTableRecordCount(dbConn, schema, "has_study_organisations"));
            if (s.has_study_references is true) LogLine(GetTableRecordCount(dbConn, schema, "study_references"));
            if (s.has_study_relationships is true) LogLine(GetTableRecordCount(dbConn, schema, "study_relationships"));
            if (s.has_study_links is true) LogLine(GetTableRecordCount(dbConn, schema, "study_links"));
            if (s.has_study_ipd_available is true) LogLine(GetTableRecordCount(dbConn, schema, "study_ipd_available"));
            if (s.has_study_countries is true) LogLine(GetTableRecordCount(dbConn, schema, "study_countries"));
            if (s.has_study_locations is true) LogLine(GetTableRecordCount(dbConn, schema, "study_locations"));
        }
        LogLine("");
        LogLine("object tables...\n");
        // these common to all databases
        LogLine(GetTableRecordCount(dbConn, schema, "data_objects"));
        LogLine(GetTableRecordCount(dbConn, schema, "object_instances"));
        LogLine(GetTableRecordCount(dbConn, schema, "object_titles"));

        // these are database dependent		

        if (s.has_object_datasets is true) LogLine(GetTableRecordCount(dbConn, schema, "object_datasets"));
        if (s.has_object_dates is true) LogLine(GetTableRecordCount(dbConn, schema, "object_dates"));
        if (s.has_object_relationships is true) LogLine(GetTableRecordCount(dbConn, schema, "object_relationships"));
        if (s.has_object_rights is true) LogLine(GetTableRecordCount(dbConn, schema, "object_rights"));
        if (s.has_object_pubmed_set is true)
        {
            LogLine(GetTableRecordCount(dbConn, schema, "citation_objects"));
            LogLine(GetTableRecordCount(dbConn, schema, "object_people"));
            LogLine(GetTableRecordCount(dbConn, schema, "object_organisations"));
            LogLine(GetTableRecordCount(dbConn, schema, "object_topics"));
            LogLine(GetTableRecordCount(dbConn, schema, "object_comments"));
            LogLine(GetTableRecordCount(dbConn, schema, "object_descriptions"));
            LogLine(GetTableRecordCount(dbConn, schema, "object_identifiers"));
            LogLine(GetTableRecordCount(dbConn, schema, "object_db_links"));
            LogLine(GetTableRecordCount(dbConn, schema, "object_publication_types"));
        }
    }

    
    public void LogDiffs(Source s)
    {
        string dbConn = s.db_conn!;

        LogLine("");
        LogLine("SD - AD Differences");
        LogLine("");
        if (s.has_study_tables is true)
        {
            LogLine(GetTableRecordCount(dbConn, "to_ad_study_recs"));
            LogLine(GetEntityRecDiffs(dbConn, "study"));
            GetStudyStats(dbConn, "recs");
            LogLine(GetTableRecordCount(dbConn, "to_ad_study_atts"));
            GetStudyStats(dbConn, "atts");
        }

        LogLine(GetTableRecordCount(dbConn, "to_ad_object_recs"));
        LogLine(GetEntityRecDiffs(dbConn, "object"));
        LogLine(GetDatasetRecDiffs(dbConn));

        GetObjectStats(dbConn, "recs");
        LogLine(GetTableRecordCount(dbConn, "to_ad_object_atts"));
        GetObjectStats(dbConn, "atts");
    }

    
    private string GetTableRecordCount(string dbConn, string tableName)
    {
        string tName = "sd." + tableName;
        string sqlString = $"select count(*) from {tName}";

        using NpgsqlConnection conn = new NpgsqlConnection(dbConn);
        int res = conn.ExecuteScalar<int>(sqlString);
        return res + " records found in sd." + tableName;
    }

    private void GetStudyStats(string dbConn, string tableType)
    {
        string tableName = "sd.to_ad_study_" + tableType;
        string sqlString = $@"select status, count(sd_sid) as num from {tableName}
                           group by status order by status;";
        GetAndWriteStats(dbConn, sqlString);
    }

    private void GetObjectStats(string dbConn, string tableType)
    {
        string tableName = "sd.to_ad_object_" + tableType;
        string sqlString = $@"select status, count(sd_oid) as num from {tableName}
                              group by status order by status;";
        GetAndWriteStats(dbConn, sqlString);
    }

    private void GetAndWriteStats(string dbConn, string sqlString)
    {
        using NpgsqlConnection conn = new(dbConn);
        List<att_stat> statusStats = conn.Query<att_stat>(sqlString).ToList();
        if (statusStats.Any())
        {
            foreach (att_stat hs in statusStats)
            {
                LogLine($"Status {hs.status}: {hs.num}");
            }
        }
        LogLine("");
    }

    private string GetEntityRecDiffs(string dbConn, string entityType)
    {
        string tableName = (entityType == "study") ? "sd.to_ad_study_recs" : "sd.to_ad_object_recs";
        string sqlString = $@"select count(*) from {tableName} 
                              where {entityType}_rec_status = 2;";
        using NpgsqlConnection conn = new(dbConn);
        int res = conn.ExecuteScalar<int>(sqlString);
        return $"{res} records found with edits to the {entityType} record itself;";
    }

    private string GetDatasetRecDiffs(string dbConn)
    {
        string sqlString = @"select count(*) from sd.to_ad_object_recs
                             where object_dataset_status = 4;";
        using NpgsqlConnection conn = new(dbConn);
        int res = conn.ExecuteScalar<int>(sqlString);
        return $"{res} records found with edits to the dataset data;";
    }

    private void Transmit(string message)
    {
        _sw!.WriteLine(message);
        Console.WriteLine(message);
    }



    public string GetTableRecordCount(string dbConn, string schema, string tableName)
    {
        string sqlString = "select count(*) from " + schema + "." + tableName;

        using NpgsqlConnection conn = new NpgsqlConnection(dbConn);
        int res = conn.ExecuteScalar<int>(sqlString);
        return res.ToString() + " records found in " + schema + "." + tableName;
    }
}