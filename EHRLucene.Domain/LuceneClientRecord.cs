﻿using EHR.CoreShared.Entities;
using EHR.CoreShared.Interfaces;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Web;
using Version = Lucene.Net.Util.Version;

namespace EHRLucene.Domain
{
    public class LuceneClientRecord
    {

        #region Properties

        private string _luceneDir;
        private FSDirectory _directoryTemp;
        private FSDirectory Directory
        {
            get
            {
                if (_directoryTemp == null) _directoryTemp = FSDirectory.Open(new DirectoryInfo(_luceneDir));
                if (IndexWriter.IsLocked(_directoryTemp)) IndexWriter.Unlock(_directoryTemp);
                //var lockFilePath = Path.Combine(LuceneDir, "write.lock");
                //if (File.Exists(lockFilePath)) File.Delete(lockFilePath);
                return _directoryTemp;
            }
        }

        #endregion

        #region Contructors

        public LuceneClientRecord(string path)
        {
            InformarPath(path);
            CreateDirectory();
        }

        #endregion

        #region Methods

        public void AddRecordsOnIndexFrom(IList<IPatient> patients)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(Directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var patient in patients)
                {
                    foreach (var record in patient.Records)
                    {
                        AddToIndex(record, patient.GetCPF(), writer);
                    }
                }

                analyzer.Close();
            }

            Optimize();
        }

        public IEnumerable<Record> SearchBy(string patientCPF)
        {
            return _inputIsNotNullOrEmpty(patientCPF) ? new List<Record>() : _SearchBy(patientCPF);
        }

        private void AddToIndex(Record record, string patientCPF, IndexWriter indexWriter)
        {
            RemoveIndex(record, patientCPF, indexWriter);
            var doc = new Document();
            AddFields(record, doc, patientCPF);
            indexWriter.AddDocument(doc);
        }

        private IEnumerable<Record> _SearchBy(string patientCPF)
        {
            patientCPF = _removeSpecialCharacters(patientCPF);

            using (var searcher = new IndexSearcher(Directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "PatientCPF" }, analyzer);
                var query = parseQuery(patientCPF, parser);
                var hits = searcher.Search(query, 10).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();

                return results;
            }
        }

        private IEnumerable<Record> _mapLuceneToDataList(IEnumerable<ScoreDoc> hits, IndexSearcher searcher)
        {
            IList<Record> records = new List<Record>();
            foreach (var hit in hits)
            {
                records.Add(_mapLuceneDocumentToData(searcher.Doc(hit.Doc)));
            }
            return records;
        }

        private Record _mapLuceneDocumentToData(Document doc)
        {
            var record = new Record()
            {
                Code = doc.Get("Code"),
                Hospital = new Hospital { Key = doc.Get("Hospital") },
            };

            return record;
        }

        private void AddFields(Record record, Document doc, string patientCPF)
        {
            doc.Add(new Field("PatientCPF", patientCPF, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Code", record.Code, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Hospital", record.Hospital.Key, Field.Store.YES, Field.Index.ANALYZED));
        }

        private void RemoveIndex(Record record, string patientCPF, IndexWriter writer)
        {
            var queryString = " (PatientCPF:" + patientCPF;
            queryString += " AND Code:" + record.Code;
            queryString += " AND Hospital:" + record.Hospital.Key + " )";

            writer.DeleteDocuments(CreateQuery(queryString));
        }

        private void InformarPath(string path)
        {
            if (string.IsNullOrEmpty(path) && HttpContext.Current != null)
            {
                if (HttpContext.Current.Request.PhysicalApplicationPath != null)
                    _luceneDir = Path.Combine(HttpContext.Current.Request.PhysicalApplicationPath, "lucene_index_record");

                return;
            }

            _luceneDir = path;
        }

        private void CreateDirectory()
        {
            if (!System.IO.Directory.Exists(_luceneDir)) System.IO.Directory.CreateDirectory(_luceneDir);
        }

        private string _removeSpecialCharacters(string searchQuery)
        {
            return searchQuery.Replace("*", "").Replace("?", "");
        }

        private Query parseQuery(string searchQuery, QueryParser parser)
        {
            Query query;
            try
            {
                query = parser.Parse(searchQuery);
            }
            catch (ParseException)
            {
                query = parser.Parse(QueryParser.Escape(searchQuery.Trim()));
            }
            return query;
        }

        private void Optimize()
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(Directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                analyzer.Close();
                writer.Optimize();
            }
        }

        private bool _inputIsNotNullOrEmpty(string input)
        {
            return string.IsNullOrEmpty(input);
        }

        private Query CreateQuery(string queryStr)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            string[] array = CreatParameters();
            var parser = new MultiFieldQueryParser(Version.LUCENE_30, array, analyzer);
            parser.DefaultOperator = QueryParser.Operator.AND;
            return parseQuery(queryStr, parser);
        }

        private string[] CreatParameters()
        {
            var parameters = new List<string>();
            parameters.Add("PatientCPF");
            parameters.Add("Code");
            parameters.Add("Hospital");
            return parameters.ToArray();
        }

        #endregion
    }
}
