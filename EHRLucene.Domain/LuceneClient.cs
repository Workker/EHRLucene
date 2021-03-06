﻿using EHR.CoreShared.Entities;
using EHR.CoreShared.Interfaces;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using Version = Lucene.Net.Util.Version;

namespace EHRLucene.Domain
{
    public class LuceneClient
    {

        #region Properties

        public string _luceneDir;
        private FSDirectory _directoryTemp;
        private FSDirectory _directory
        {
            get
            {
                if (_directoryTemp == null) _directoryTemp = FSDirectory.Open(new DirectoryInfo(_luceneDir));
                if (IndexWriter.IsLocked(_directoryTemp)) IndexWriter.Unlock(_directoryTemp);
                var lockFilePath = Path.Combine(_luceneDir, "write.lock");
                //if (File.Exists(lockFilePath)) File.Delete(lockFilePath);
                return _directoryTemp;
            }
        }

        #endregion

        #region Contructors

        public LuceneClient(string path)
        {
            InformarPath(path);
            CreateDirectory();
        }

        #endregion

        public void CreateDirectory()
        {
            if (!System.IO.Directory.Exists(_luceneDir)) System.IO.Directory.CreateDirectory(_luceneDir);
        }

        public void AddUpdateLuceneIndex(Patient patients)
        {
            AddUpdateLuceneIndex(new List<Patient> { patients });
            Optimize();
        }

        public void AddUpdateLuceneIndex(IEnumerable<IPatient> sampleDatas)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var sampleData in sampleDatas) _addToLuceneIndex(sampleData, writer);
                analyzer.Close();
            }
        }

        public IPatient SearchBy(string cpf)
        {
            return _inputIsNotNullOrEmpty(cpf) ? new Patient() : _SearchBy(cpf);
        }

        public IEnumerable<IPatient> SimpleSearch(string input)
        {
            return _inputIsNotNullOrEmpty(input) ? new List<IPatient>() : _SimpleSearch(input);

        }

        public IEnumerable<IPatient> AdvancedSearch(IPatient patient, List<string> hospitalKeys)
        {
            return patient == null ? new List<IPatient>() : _AdvancedSearch(patient, hospitalKeys);
        }

        public IEnumerable<IPatient> AdvancedSearch(List<IPatient> patients)
        {
            return patients == null ? new List<IPatient>() : _AdvancedSearch(patients);
        }


        //TODO: Remover se não for usado
        //private MultiFieldQueryParser CreateParser(StandardAnalyzer analyzer)
        //{
        //    var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Name", "DateBirthday" }, analyzer);
        //    parser.DefaultOperator = QueryParser.Operator.AND;
        //    return parser;
        //}


        private IPatient _SearchBy(string searchQuery)
        {
            searchQuery = _removeSpecialCharacters(searchQuery);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "CPF" }, analyzer);
                var query = parseQuery(searchQuery, parser);
                var hits = searcher.Search(query, 10).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();

                return results.First();
            }
        }

        private IEnumerable<IPatient> _SimpleSearch(string searchQuery)
        {
            searchQuery = _removeSpecialCharacters(searchQuery);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Name", "CPF" }, analyzer);
                var query = parseQuery(searchQuery, parser);
                var hits = searcher.Search(query, null, 10, new Sort(new SortField("Name", SortField.STRING))).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();

                return results;

            }
        }

        private string _removeSpecialCharacters(string searchQuery)
        {
            return searchQuery.Replace("*", "").Replace("?", "");
        }

        private bool _inputIsNotNullOrEmpty(string input)
        {
            return string.IsNullOrEmpty(input);
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

        private IEnumerable<IPatient> _mapLuceneToDataList(IEnumerable<ScoreDoc> hits, IndexSearcher searcher)
        {

            IList<IPatient> patients = new List<IPatient>();
            foreach (var hit in hits)
            {
                patients.Add(_mapLuceneDocumentToData(searcher.Doc(hit.Doc)));
            }
            //hits.Select(hit => _mapLuceneDocumentToData(searcher.Doc(hit.Doc))).ToList();
            return patients;
        }

        private IPatient _mapLuceneDocumentToData(Document doc)
        {
            var patient = new Patient()
            {
                Id = doc.Get("Id"),
                Name = doc.Get("Name"),
                CPF = doc.Get("CPF"),
                Hospital = new Hospital { Key = doc.Get("Hospital") },
            };

            if (!string.IsNullOrEmpty(doc.Get("DateBirthday")))
            {
                patient.DateBirthday = DateTime.Parse(doc.Get("DateBirthday"), CultureInfo.GetCultureInfo("pt-br") );
            }
            if (!string.IsNullOrEmpty(doc.Get("CheckOutDate")))
            {
                patient.DateBirthday = DateTime.Parse(doc.Get("CheckOutDate"), CultureInfo.GetCultureInfo("pt-br"));
            }
            if (!string.IsNullOrEmpty(doc.Get("EntryDate")))
            {
                patient.EntryDate = DateTime.Parse(doc.Get("EntryDate"), CultureInfo.GetCultureInfo("pt-br"));
            }


            return patient;
        }

        private void Optimize()
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                analyzer.Close();
                writer.Optimize();
            }
        }

        private void InformarPath(string path)
        {
            if (string.IsNullOrEmpty(path) && HttpContext.Current != null)
            {
                if (HttpContext.Current.Request.PhysicalApplicationPath != null)
                    _luceneDir = Path.Combine(HttpContext.Current.Request.PhysicalApplicationPath, "lucene_index_patient");

                return;
            }

            _luceneDir = path;
        }

        private string TreatCharacters(IPatient patient, List<string> hospitalKeys)
        {
            var str = "Name:";
            str += _removeSpecialCharacters(patient.Name);
            if (!string.IsNullOrEmpty(patient.DateBirthday.ToString()) && patient.DateBirthday.ToString() != "//")
            {
                str += " DateBirthday:";
                str += _removeSpecialCharacters(patient.DateBirthday.ToString()).Replace(" 00:00:00", ""); ;

            }

            var i = 1;
            foreach (var h in hospitalKeys)
            {
                if (hospitalKeys.Count > 1 && i < hospitalKeys.Count)
                {
                    str += " Hospital:" + h + " OR ";
                }
                else
                {
                    str += " Hospital:" + h;
                }
                i++;
            }

            return str;
        }

        private string TreatCharacters(List<IPatient> patients)
        {
            var i = 1;
            string str = "";

            foreach (var p in patients)
            {
                if (patients.Count > 1 && i < patients.Count)
                {
                    str += " (CPF:" + p.GetCPF() + ") OR ";
                }
                else
                {
                    str += " (CPF:" + p.GetCPF() + ")";
                }
                i++;
            }

            return str;
        }

        private IEnumerable<IPatient> _AdvancedSearch(IPatient searchQuery, List<string> hospitalKeys)
        {
            var searchQueryStr = TreatCharacters(searchQuery, hospitalKeys);

            using (var searcher = new IndexSearcher(_directory, false))
            {

                var analyzer = new StandardAnalyzer(Version.LUCENE_30);

                string[] array = CreatParameters(searchQuery, hospitalKeys);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, array, analyzer);
                if (array.Count() > 1)
                    parser.DefaultOperator = QueryParser.Operator.AND;

                var query = parseQuery(searchQueryStr, parser);
                var hits = searcher.Search(query, null, 5000000, Sort.RELEVANCE).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();

                return results;
            }
        }

        private IEnumerable<IPatient> _AdvancedSearch(List<IPatient> patients)
        {
            var searchQueryStr = TreatCharacters(patients);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);

                string[] array = CreatParameters(patients);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, array, analyzer);
                parser.DefaultOperator = QueryParser.Operator.AND;

                var query = parseQuery(searchQueryStr, parser);
                var hits = searcher.Search(query, null, 5000000, Sort.RELEVANCE).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();

                return results;
            }
        }

        private string[] CreatParameters(List<IPatient> patients)
        {
            var parameters = new List<string> { "CPF" };
            return parameters.ToArray();
        }

        private string[] CreatParameters(IPatient searchQuery, List<string> hospitalKeys)
        {
            var parameters = new List<string>();

            if (!string.IsNullOrEmpty(searchQuery.Name))
                parameters.Add("Name");

            if (!string.IsNullOrEmpty(searchQuery.DateBirthday.ToString()) && searchQuery.DateBirthday.ToString() != "//")
                parameters.Add("DateBirthday");

            if (hospitalKeys != null && hospitalKeys.Count > 0)
                parameters.Add("Hospital");

            return parameters.ToArray();
        }

        private void _addToLuceneIndex(IPatient patient, IndexWriter writer)
        {
            RemoveIndex(patient, writer);
            var doc = new Document();
            AddFields(patient, doc);
            writer.AddDocument(doc);
        }

        private void AddFields(IPatient patient, Document doc)
        {
            doc.Add(new Field("Id", patient.Id.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Name", patient.Name, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Hospital", patient.Hospital.Key, Field.Store.YES, Field.Index.ANALYZED));

            if (patient.CheckOutDate.HasValue)
                doc.Add(new Field("CheckOutDate", patient.CheckOutDate.Value.ToShortDateString(), Field.Store.YES, Field.Index.NOT_ANALYZED));

            if (patient.EntryDate.HasValue)
                doc.Add(new Field("EntryDate", patient.EntryDate.Value.ToShortDateString(), Field.Store.YES, Field.Index.NOT_ANALYZED));

            if (!string.IsNullOrEmpty(patient.CPF))
                doc.Add(new Field("CPF", patient.GetCPF(), Field.Store.YES, Field.Index.ANALYZED));

            if (!string.IsNullOrEmpty(patient.DateBirthday.ToString()))
                doc.Add(new Field("DateBirthday", patient.DateBirthday.ToString(), Field.Store.YES, Field.Index.ANALYZED));
        }

        private void RemoveIndex(IPatient patient, IndexWriter writer)
        {
            var searchQuery = new TermQuery(new Term("Id", patient.Id.ToString(CultureInfo.InvariantCulture)));
            writer.DeleteDocuments(searchQuery);
        }
    }
}
