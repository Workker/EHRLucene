using EHR.CoreShared.Entities;
using EHR.CoreShared.Interfaces;
using EHRIntegracao.Domain.Repository;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System.Collections.Generic;
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

        public void AddRecordsOnIndexFromPatient(IPatient patient)
        {
            foreach (var sampleData in patient.Records) AddToIndex(sampleData, patient.Id);
        }

        public void AddToIndex(Record record, string patientId)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(Directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                RemoveIndex(record, writer);
                var doc = new Document();
                AddFields(record, doc, patientId);
                writer.AddDocument(doc);
                analyzer.Close();
            }
            Optimize();
        }

        public void AddToIndex(IEnumerable<Record> records, string patientId)
        {
            foreach (var sampleData in records) AddToIndex(sampleData, patientId);
        }

        public IEnumerable<Record> SearchBy(string patientId)
        {
            return _inputIsNotNullOrEmpty(patientId) ? new List<Record>() : _SearchBy(patientId);
        }

        private IEnumerable<Record> _SearchBy(string patientId)
        {
            patientId = _removeSpecialCharacters(patientId);

            using (var searcher = new IndexSearcher(Directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "PatientId" }, analyzer);
                var query = parseQuery(patientId, parser);
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
            //hits.Select(hit => _mapLuceneDocumentToData(searcher.Doc(hit.Doc))).ToList();
            return records;
        }

        private Record _mapLuceneDocumentToData(Document doc)
        {
            var repository = new Hospitals();
            var hospital = repository.GetBy(doc.Get("Hospital"));

            var record = new Record()
            {
                Code = doc.Get("Code"),
                Hospital = hospital,
            };

            return record;
        }

        private void AddFields(Record record, Document doc, string patientId)
        {
            doc.Add(new Field("Id", patientId, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Code", record.Code, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Hospital", record.Hospital.Key, Field.Store.YES, Field.Index.ANALYZED));
        }

        private void RemoveIndex(Record record, IndexWriter writer)
        {
            var searchQuery = new TermQuery(new Term("Code", record.Code));
            writer.DeleteDocuments(searchQuery);
        }

        private void InformarPath(string path)
        {
            _luceneDir = string.IsNullOrEmpty(path) ? Path.Combine(HttpContext.Current.Request.PhysicalApplicationPath ?? "C:\\", "lucene_index_record") : path;
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

        #endregion
    }
}
