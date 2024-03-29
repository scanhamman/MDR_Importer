﻿using Dapper;
using Npgsql;

namespace MDR_Importer;

public class TestingDataLayer : ITestingDataLayer
{
    private readonly  ICredentials _credentials;
    private readonly string _dbConn;
    private readonly ILoggingHelper _loggingHelper;

    public TestingDataLayer(ICredentials credentials, ILoggingHelper loggingHelper)
    {
        _credentials = credentials;        
        _loggingHelper = loggingHelper;       
        _dbConn = credentials.GetConnectionString("test", true);
    }

    public Credentials Credentials => (Credentials)_credentials;

    
    public IEnumerable<int> ObtainTestSourceIDs()
    {
        string sqlString = @"select distinct source_id 
                             from expected.source_studies
                             union
                             select distinct source_id 
                             from expected.source_objects;";

        using var conn = new NpgsqlConnection(_dbConn);
        return conn.Query<int>(sqlString);
    }


    public void SetUpADCompositeTables()
    {
        ADCompTableBuilder atb = new ADCompTableBuilder(_dbConn);
        atb.BuildStudyTables();
        atb.BuildObjectTables();
        _loggingHelper.LogLine("Composite AD tables established");
    }

   

    public void RetrieveSDData(Source source)
    {
        RetrieveSDDataBuilder rs_db = new RetrieveSDDataBuilder(source);
        rs_db.DeleteExistingSDStudyData();
        rs_db.DeleteExistingSDObjectData();
        rs_db.RetrieveStudyData();
        rs_db.RetrieveObjectData();
        _loggingHelper.LogLine("SD test data for source " + source.id + " retrieved from CompSD");
    }


    public void RetrieveADData(Source source)
    {
        RetrieveADDataBuilder ra_db = new RetrieveADDataBuilder(source);
        ra_db.DeleteExistingADStudyData();
        ra_db.DeleteExistingADObjectData();
        ra_db.RetrieveStudyData();
        ra_db.RetrieveObjectData();
        _loggingHelper.LogLine("AD test data for source " + source.id + " retrieved from CompAD");
    }


    public void TransferADDataToComp(Source source)
    {
        TransferADDataBuilder tdb = new TransferADDataBuilder(source);
        tdb.DeleteExistingStudyData();
        tdb.DeleteExistingObjectData();
        _loggingHelper.LogLine("Any existing AD test data for source " + source.id + " removed from CompAD");
        tdb.TransferStudyData();
        tdb.TransferObjectData();
        _loggingHelper.LogLine("New AD test data for source " + source.id + " added to CompAD");
    }


    public void ApplyScriptedADChanges()
    {
        //AdChangesBuilder tdb = new AdChangesBuilder(_dbConn);

        /*
        tdb.DeleteExistingStudyData();
        tdb.DeleteExistingObjectData();
        _logger.Information("Any existing SD test data for source " + source.id + " removed from CompSD");

        tdb.TransferStudyData();
        tdb.TransferObjectData();
        _logger.Information("New SD test data for source " + source.id + " added to CompSD");
        */
    }


    public void ConstructDiffReport()
    {
        TestReportBuilder tdb = new TestReportBuilder(_dbConn);

        if (tdb.CompareStudyRecordCounts())
        {
            tdb.CompareStudyRecords();
            tdb.CompareStudyAttributes();
           // tdb.CompareStudyHashes();
        }

        if (tdb.CompareObjectRecordCounts())
        {
            tdb.CompareObjectRecords();
            tdb.CompareObjectAttributes();
          //  tdb.CompareObjectHashes();
        }

        tdb.CompareFullHashes();


        tdb.Close();

    }

}