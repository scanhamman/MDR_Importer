﻿using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
namespace MDR_Importer;

public class MonDataLayer : IMonDataLayer
{
    private readonly ICredentials _credentials;
    private readonly ILoggingHelper _loggingHelper;    
    private readonly string? _sqlFileSelectString;
    private readonly string? _db_conn;
    
    public MonDataLayer(ICredentials credentials, ILoggingHelper loggingHelper)
    {
        _credentials = credentials;
        _loggingHelper = loggingHelper;

        _db_conn = credentials.GetConnectionString("mon", false);

        _sqlFileSelectString = "select id, source_id, sd_id, remote_url, last_revised, ";
        _sqlFileSelectString += " assume_complete, download_status, local_path, last_saf_id, last_downloaded, ";
        _sqlFileSelectString += " last_harvest_id, last_harvested, last_import_id, last_imported ";
    }

    public Credentials Credentials => (Credentials)_credentials;

    public string GetConnectionString(string database_name, bool using_test_data) =>
                                     _credentials.GetConnectionString(database_name, using_test_data);
           
    public bool SourceIdPresent(int? sourceId)
    {
        string sqlString = "Select id from sf.source_parameters where id = " + sourceId.ToString();
        using NpgsqlConnection conn = new(_db_conn);
        int res = conn.QueryFirstOrDefault<int>(sqlString);
        return (res != 0);
    }
    
    public Source? FetchSourceParameters(int? sourceId)
    {
        using NpgsqlConnection conn = new(_db_conn);
        return conn.Get<Source>(sourceId);
    }

    public int GetNextImportEventId()
    {
        using NpgsqlConnection conn = new(_db_conn);
        string sqlString = "select max(id) from sf.import_events ";
        int lastId = conn.ExecuteScalar<int>(sqlString);
        return (lastId == 0) ? 10001 : lastId + 1;
    }

    
    public IEnumerable<StudyFileRecord> FetchStudyFileRecords(int? sourceId, int harvestTypeId = 1, DateTime? cutoffDate = null)
    {
        string sqlString = _sqlFileSelectString!;
        sqlString += " from sf.source_data_studies ";
        sqlString += GetWhereClause(sourceId, harvestTypeId, cutoffDate);

        using NpgsqlConnection conn = new NpgsqlConnection(_db_conn);
        return conn.Query<StudyFileRecord>(sqlString);
    }


    public IEnumerable<ObjectFileRecord> FetchObjectFileRecords(int? sourceId, int harvestTypeId = 1, DateTime? cutoffDate = null)
    {
        string sqlString = _sqlFileSelectString!;
        sqlString += " from sf.source_data_objects";
        sqlString += GetWhereClause(sourceId, harvestTypeId, cutoffDate);

        using NpgsqlConnection conn = new(_db_conn);
        return conn.Query<ObjectFileRecord>(sqlString);
    }


    public int FetchFileRecordsCount(int? sourceId, string sourceType, int harvestTypeId = 1, DateTime? cutoffDate = null)
    {
        string sqlString = "select count(*) ";
        sqlString += sourceType.ToLower() == "study" ? "from sf.source_data_studies"
                                             : "from sf.source_data_objects";
        sqlString += GetWhereClause(sourceId, harvestTypeId, cutoffDate);

        using NpgsqlConnection conn = new(_db_conn);
        return conn.ExecuteScalar<int>(sqlString);
    }


    public IEnumerable<StudyFileRecord> FetchStudyFileRecordsByOffset(int? sourceId, int offsetNum,
                                  int amount, int harvestTypeId = 1, DateTime? cutoffDate = null)
    {
        string sqlString = _sqlFileSelectString!;
        sqlString += " from sf.source_data_studies ";
        sqlString += GetWhereClause(sourceId, harvestTypeId, cutoffDate);  
        sqlString += " offset " + offsetNum + " limit " + amount;

        using NpgsqlConnection conn = new NpgsqlConnection(_db_conn);
        return conn.Query<StudyFileRecord>(sqlString);
    }

    public IEnumerable<ObjectFileRecord> FetchObjectFileRecordsByOffset(int? sourceId, int offsetNum,
                                 int amount, int harvestTypeId = 1, DateTime? cutoffDate = null)
    {
        string sqlString = _sqlFileSelectString!;
        sqlString += " from sf.source_data_objects ";
        sqlString += GetWhereClause(sourceId, harvestTypeId, cutoffDate);
        sqlString += " offset " + offsetNum + " limit " + amount;

        using NpgsqlConnection conn = new(_db_conn);
        return conn.Query<ObjectFileRecord>(sqlString);
    }

    private string GetWhereClause(int? sourceId, int harvestTypeId, DateTime? cutoffDate = null)
    {
        string whereClause = "";
        if (harvestTypeId == 1)
        {
            // Count all files.
            whereClause = " where source_id = " + sourceId.ToString();
        }
        else if (harvestTypeId == 2)
        {
            // Count only those files that have been revised (or added) on or since the cutoff date.
            whereClause = " where source_id = " + sourceId.ToString() + " and last_revised >= '" + cutoffDate + "'";
        }
        else if (harvestTypeId == 3)
        {
            // For sources with no revision date - Count files unless assumed complete has been set
            // as true (default is null) in which case no further change is expected.
            whereClause = " where source_id = " + sourceId.ToString() + " and assume_complete is null";
        }

        whereClause += " and local_path is not null";
        whereClause += " order by local_path";
        return whereClause;
    }

    // get record of interest
    public StudyFileRecord? FetchStudyFileRecord(string sdId, int? sourceId, string sourceType)
    {
        using NpgsqlConnection conn = new(_db_conn);
        string sqlString = _sqlFileSelectString!;
        sqlString += " from sf.source_data_studies";
        sqlString += " where sd_id = '" + sdId + "' and source_id = " + sourceId.ToString();
        return conn.Query<StudyFileRecord>(sqlString).FirstOrDefault();
    }

    public ObjectFileRecord? FetchObjectFileRecord(string sdId, int? sourceId, string sourceType)
    {
        using NpgsqlConnection conn = new(_db_conn);
        string sqlString = _sqlFileSelectString!;
        sqlString += " from sf.source_data_objects";
        sqlString += " where sd_id = '" + sdId + "' and source_id = " + sourceId.ToString();
        return conn.Query<ObjectFileRecord>(sqlString).FirstOrDefault();
    }

    public void UpdateFileRecLastImported(int id, string sourceType)
    {
        using NpgsqlConnection conn = new(_db_conn);
        string sqlString = sourceType.ToLower() == "study" ? "update sf.source_data_studies"
            : "update sf.source_data_objects";
        sqlString += " set last_imported = current_timestamp";
        sqlString += " where id = " + id.ToString();
        conn.Execute(sqlString);
    }

    public int StoreImportEvent(ImportEvent import)
    {
        import.time_ended = DateTime.Now;
        using NpgsqlConnection conn = new(_db_conn);
        return (int)conn.Insert(import);
    }

    public bool CheckIfFullHarvest(int? sourceId)
    {
        string sqlString = $@"select type_id from sf.harvest_events
                      where source_id = {sourceId} and time_ended = 
                          (select max(time_ended) from sf.harvest_events 
                           where source_id = {sourceId})";

        using NpgsqlConnection conn = new(_db_conn);
        int res = conn.ExecuteScalar<int>(sqlString);
        return (res == 1);   // harvest type 1 = all records
    }

}