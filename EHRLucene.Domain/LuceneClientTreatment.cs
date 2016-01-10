using EHR.CoreShared.Entities;
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
using System.Web;
using Version = Lucene.Net.Util.Version;

namespace EHRLucene.Domain
{
    public class LuceneClientTreatment
    {

        public LuceneClientTreatment(string path)
        {
            InformarPath(path);
            CriarDiretorio();

        }

        private void InformarPath(string path)
        {
            if (string.IsNullOrEmpty(path) && HttpContext.Current != null)
            {
                if (HttpContext.Current.Request.PhysicalApplicationPath != null)
                    _luceneDir = Path.Combine(HttpContext.Current.Request.PhysicalApplicationPath, "lucene_index_treatment");

                return;
            }

            _luceneDir = path;
        }

        public void CriarDiretorio()
        {
            if (!System.IO.Directory.Exists(_luceneDir)) System.IO.Directory.CreateDirectory(_luceneDir);
        }

        public string _luceneDir;
        private FSDirectory _directoryTemp;
        private FSDirectory _directory
        {
            get
            {
                if (_directoryTemp == null) _directoryTemp = FSDirectory.Open(new DirectoryInfo(_luceneDir));
                if (IndexWriter.IsLocked(_directoryTemp)) IndexWriter.Unlock(_directoryTemp);
                var lockFilePath = Path.Combine(_luceneDir, "write.lock");
                return _directoryTemp;
            }
        }

        public void AddUpdateLuceneIndex(ITreatment patients)
        {
            AddUpdateLuceneIndex(new List<ITreatment> { patients });
            Optimize();
        }

        public void AddUpdateLuceneIndex(IEnumerable<ITreatment> sampleDatas)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var sampleData in sampleDatas) _addToLuceneIndex(sampleData, writer);

                analyzer.Close();
            }
        }

        private void _addToLuceneIndex(ITreatment treatment, IndexWriter writer)
        {
            //Não precisa remover o tratamento, pois existem varios tratamentos com o id igual.
            //     RemoveIndex(treatment, writer);
            var doc = new Document();
            AddFields(treatment, doc);
            writer.AddDocument(doc);
        }

        private void AddFields(ITreatment treatment, Document doc)
        {
            doc.Add(new Field("Id", treatment.Id.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Hospital", treatment.Hospital.Key, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("CheckOutDate", treatment.CheckOutDate.ToShortDateString(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("EntryDate", treatment.EntryDate.ToShortDateString(), Field.Store.YES, Field.Index.ANALYZED));
        }

        private void RemoveIndex(ITreatment treatment, IndexWriter writer)
        {
            string str = "";

            str += " (Hospital:" + treatment.Hospital;
            if (treatment.CheckOutDate != DateTime.MinValue)
                str += " AND CheckOutDate:" + treatment.CheckOutDate.ToShortDateString();
            str += " AND Id:" + treatment.Id;
            str += " AND EntryDate:" + treatment.EntryDate.ToShortDateString() + " )";

            writer.DeleteDocuments(CreateQuery(str));
        }

        public IEnumerable<ITreatment> SimpleSearch(string input)
        {
            return _inputIsNotNullOrEmpty(input) ? new List<ITreatment>() : _SimpleSearch(input);

        }

        public IEnumerable<ITreatment> SearchBy(List<Record> records)
        {
            try
            {
                return _SearchBy(records);
            }
            catch (Exception)
            {

                throw;
            }

        }

        private IEnumerable<ITreatment> _SearchBy(List<Record> records)
        {
            var searchQueryStr = TreatCharacters(records);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);

                string[] array = CreatParameters(records);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, array, analyzer);
                parser.DefaultOperator = QueryParser.Operator.AND;

                var query = parseQuery(searchQueryStr, parser);
                var hits = searcher.Search(query, null, 300, Sort.INDEXORDER).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();

                return results;
            }
        }

        private string[] CreatParameters(List<Record> hospital)
        {
            var parameters = new List<string> { "Id", "Hospital" };

            return parameters.ToArray();
        }

        private string TreatCharacters(List<Record> records)
        {
            var str = "";

            var i = 1;
            foreach (var record in records)
            {
                if (records.Count > 1 && i < records.Count)
                {
                    str += " (Id:" + record.Code + " AND Hospital:" + record.Hospital.Key + " ) OR ";
                }
                else
                {
                    str += " Id:" + record.Code + " AND Hospital:" + record.Hospital.Key;
                }
                i++;
            }

            //str += " Hospital: " + records.FirstOrDefault().Hospital.Key;

            return str;
        }

        public IEnumerable<ITreatment> AdvancedPeriodicSearch(List<ITreatment> treatments)
        {
            try
            {
                return _AdvancedSearch(treatments);
            }
            catch (Exception)
            {

                throw;
            }

        }

        private string TreatCharacters(List<ITreatment> treatmentDtos)
        {
            var str = "";

            var i = 1;
            foreach (var h in treatmentDtos)
            {

                if (treatmentDtos.Count > 1 && i < treatmentDtos.Count)
                {
                    str += " (Hospital:" + h.Hospital;
                    str += " AND CheckOutDate:" + h.CheckOutDate.ToShortDateString();

                    str += " AND Id:" + h.Id;
                    str += " AND EntryDate:" + h.EntryDate.ToShortDateString() +
                      ") OR ";
                }
                else
                {
                    str += " (Hospital:" + h.Hospital;
                    //if (h.CheckOutDate != DateTime.MinValue)
                    str += " AND CheckOutDate:" + h.CheckOutDate.ToShortDateString();
                    str += " AND Id:" + h.Id;
                    str += " AND EntryDate:" + h.EntryDate.ToShortDateString() + " )";
                }
                i++;
            }

            return str;
        }

        private Query CreateQuery(string queryStr)
        {
            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);

                string[] array = CreatParameters();
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, array, analyzer);
                parser.DefaultOperator = QueryParser.Operator.AND;

                return parseQuery(queryStr, parser);
            }
        }

        private IEnumerable<ITreatment> _AdvancedSearch(List<ITreatment> treatmentDtos)
        {
            var searchQueryStr = TreatCharacters(treatmentDtos);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);

                string[] array = CreatParameters();
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, array, analyzer) { DefaultOperator = QueryParser.Operator.AND };

                var query = parseQuery(searchQueryStr, parser);
                var hits = searcher.Search(query, null, 300, Sort.RELEVANCE).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();

                return results;
            }
        }

        private string[] CreatParameters()
        {
            var parameters = new List<string>();
            parameters.Add("Id");
            parameters.Add("Hospital");
            parameters.Add("CheckOutDate");
            parameters.Add("EntryDate");
            return parameters.ToArray();
        }

        private MultiFieldQueryParser CreateParser(StandardAnalyzer analyzer)
        {
            var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Id" }, analyzer);
            parser.DefaultOperator = QueryParser.Operator.AND;
            return parser;
        }

        private IEnumerable<ITreatment> _SimpleSearch(string searchQuery)
        {
            searchQuery = _removeSpecialCharacters(searchQuery);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Id" }, analyzer);
                var query = parseQuery(searchQuery, parser);
                var hits = searcher.Search(query, null, 10, Sort.RELEVANCE).ScoreDocs;
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

        private IEnumerable<ITreatment> _mapLuceneToDataList(IEnumerable<ScoreDoc> hits, IndexSearcher searcher)
        {
            try
            {
                IList<ITreatment> treatmentDtos = new List<ITreatment>();
                foreach (var hit in hits)
                {
                    treatmentDtos.Add(_mapLuceneDocumentToData(searcher.Doc(hit.Doc)));
                }
                //hits.Select(hit => _mapLuceneDocumentToData(searcher.Doc(hit.Doc))).ToList();
                return treatmentDtos;
            }
            catch (Exception)
            {
                throw;
            }

        }

        private ITreatment _mapLuceneDocumentToData(Document doc)
        {
            var treatment = new Treatment()
            {
                Id = doc.Get("Id"),
                Hospital = new Hospital { Key = doc.Get("Hospital") },
                CheckOutDate = Convert.ToDateTime(doc.Get("CheckOutDate")),
                EntryDate = Convert.ToDateTime(doc.Get("EntryDate")),
            };
            return treatment;
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

    }
}