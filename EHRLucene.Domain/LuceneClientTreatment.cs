using System.Globalization;
using EHR.CoreShared.Entities;
using EHR.CoreShared.Interfaces;
using EHRIntegracao.Domain.Repository;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            _luceneDir = Path.Combine("lucene_index_Treatment");
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

        public IEnumerable<ITreatment> AdvancedSearch(List<Record> medicalRecords)
        {
            try
            {
                return _AdvancedSearch(medicalRecords);
            }
            catch (Exception)
            {

                throw;
            }

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

        private string TreatCharacters(List<Record> medicalRecords)
        {
            var str = "";

            var i = 1;
            foreach (var h in medicalRecords.Select(m => m.Code))
            {
                if (medicalRecords.Count > 1 && i < medicalRecords.Count)
                {
                    str += " (Id:" + h + " ) OR ";
                }
                else
                {
                    str += " Id:" + h;
                }
                i++;
            }

            str += " Hospital: " + medicalRecords.FirstOrDefault().Hospital.ToString().ToLower();

            return str;
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

        private IEnumerable<ITreatment> _AdvancedSearch(List<Record> medicalRecords)
        {
            var searchQueryStr = TreatCharacters(medicalRecords);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);

                string[] array = CreatParameters(medicalRecords);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, array, analyzer);
                parser.DefaultOperator = QueryParser.Operator.AND;

                var query = parseQuery(searchQueryStr, parser);
                var hits = searcher.Search(query, null, 300, Sort.RELEVANCE).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();

                return results;
            }
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

        private string[] CreatParameters(List<Record> hospital)
        {
            var parameters = new List<string>();

            parameters.Add("Id");
            parameters.Add("Hospital");
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
            var repository = new Hospitals();
            var hospital = repository.GetBy(doc.Get("Hospital"));
            var treatment = new Treatment()
            {
                Id = doc.Get("Id"),
                Hospital = hospital,
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