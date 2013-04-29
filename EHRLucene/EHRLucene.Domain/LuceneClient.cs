using EHRIntegracao.Domain.Factorys;
using EHRIntegracao.Domain.Services.DTO;
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
using System.Web;
using Version = Lucene.Net.Util.Version;

namespace EHRLucene.Domain
{
    public class LuceneClient
    {
<<<<<<< HEAD

        public LuceneClient(string path)
        {
            InformarPath(path);
            CriarDiretorio();

        }

        private void InformarPath(string path)
        {
            _luceneDir = Path.Combine(HttpContext.Current.Request.PhysicalApplicationPath,"lucene_index");
        }

        public void CriarDiretorio()
        {
            if (!System.IO.Directory.Exists(_luceneDir)) System.IO.Directory.CreateDirectory(_luceneDir);
        }
=======
        #region Properties
>>>>>>> TA758 - Pronto

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

        public void AddUpdateLuceneIndex(PatientDTO patients)
        {
            AddUpdateLuceneIndex(new List<PatientDTO> { patients });
            Optimize();
        }

        public void AddUpdateLuceneIndex(IEnumerable<IPatientDTO> sampleDatas)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var sampleData in sampleDatas) _addToLuceneIndex(sampleData, writer);
                analyzer.Close();
                writer.Dispose();
            }
        }

<<<<<<< HEAD
        private void _addToLuceneIndex(IPatientDTO patient, IndexWriter writer)
        {
            RemoveIndex(patient, writer);
            var doc = new Document();
            AddFields(patient, doc);
            writer.AddDocument(doc);
        }

        private void AddFields(IPatientDTO patient, Document doc)
        {
            doc.Add(new Field("Id", patient.Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("Name", patient.Name, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Hospital", patient.Hospital.ToString(), Field.Store.YES, Field.Index.ANALYZED));

            if (!string.IsNullOrEmpty(patient.CPF))
                doc.Add(new Field("CPF", patient.CPF, Field.Store.YES, Field.Index.ANALYZED));

            if (patient.DateBirthday.HasValue)
                doc.Add(new Field("DateBirthday", patient.DateBirthday.Value.ToShortDateString(), Field.Store.YES, Field.Index.ANALYZED));
        }

        private void RemoveIndex(IPatientDTO patient, IndexWriter writer)
=======
        public IPatientDTO SearchBy(string id)
>>>>>>> TA758 - Pronto
        {
            return _inputIsNotNullOrEmpty(id) ? new PatientDTO() : _SearchBy(id);
        }

        public IEnumerable<IPatientDTO> SimpleSearch(string input)
        {
            return _inputIsNotNullOrEmpty(input) ? new List<IPatientDTO>() : _SimpleSearch(input);

        }

        public IEnumerable<IPatientDTO> AdvancedSearch(IPatientDTO patient, List<string> hospital)
        {
            return patient == null ? new List<IPatientDTO>() : _AdvancedSearch(patient, hospital);
        }

<<<<<<< HEAD
        private string TreatCharacters(IPatientDTO patient, List<string> hospital)
        {
            var str = "Name:";
            str += _removeSpecialCharacters(patient.Name);
            if (patient.DateBirthday.HasValue && patient.DateBirthday.Value.ToShortDateString() != "//")
            {
                str += " DateBirthday:";
                str += _removeSpecialCharacters(patient.DateBirthday.Value.ToShortDateString());
            }

            var i = 1;
            foreach (var h in hospital)
            {
                if (hospital.Count > 1 && i < hospital.Count)
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
=======
        //TODO: Remover se não for usado
        //private MultiFieldQueryParser CreateParser(StandardAnalyzer analyzer)
        //{
        //    var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Name", "DateBirthday" }, analyzer);
        //    parser.DefaultOperator = QueryParser.Operator.AND;
        //    return parser;
        //}
>>>>>>> TA758 - Pronto

        private IPatientDTO _SearchBy(string searchQuery)
        {
            searchQuery = _removeSpecialCharacters(searchQuery);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Id" }, analyzer);
                var query = parseQuery(searchQuery, parser);
                var hits = searcher.Search(query,10).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();
                searcher.Dispose();

                return results.First();
            }
        }

<<<<<<< HEAD
        private string[] CreatParameters(IPatientDTO searchQuery, List<string> hospital)
        {
            var parameters = new List<string>();

            if (!string.IsNullOrEmpty(searchQuery.Name))
                parameters.Add("Name");

            if (searchQuery.DateBirthday.HasValue && searchQuery.DateBirthday.Value.ToShortDateString() != "//")
                parameters.Add("DateBirthday");

            if (hospital != null && hospital.Count > 0)
                parameters.Add("Hospital");

            return parameters.ToArray();
        }



        private MultiFieldQueryParser CreateParser(StandardAnalyzer analyzer)
        {
            var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Name", "DateBirthday" }, analyzer);
            parser.DefaultOperator = QueryParser.Operator.AND;
            return parser;
        }


=======
>>>>>>> TA758 - Pronto
        private IEnumerable<IPatientDTO> _SimpleSearch(string searchQuery)
        {
            searchQuery = _removeSpecialCharacters(searchQuery);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Name", "CPF" }, analyzer);
                var query = parseQuery(searchQuery, parser);
                var hits = searcher.Search(query, null, 10, Sort.RELEVANCE).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();
                searcher.Dispose();

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

        private IEnumerable<IPatientDTO> _mapLuceneToDataList(IEnumerable<ScoreDoc> hits, IndexSearcher searcher)
        {
            return hits.Select(hit => _mapLuceneDocumentToData(searcher.Doc(hit.Doc))).ToList();
        }

        private IPatientDTO _mapLuceneDocumentToData(Document doc)
        {
            DbEnum valor;
            var enumHospital = Enum.TryParse(doc.Get("Hospital"), true, out valor);

            var patient = new PatientDTO()
            {
                Id = doc.Get("Id"),
                Name = doc.Get("Name"),
                Hospital = enumHospital ? valor : DbEnum.sumario,
<<<<<<< HEAD
                
=======
                DateBirthday = Convert.ToDateTime(doc.Get("DateBirthday")),
>>>>>>> TA758 - Pronto
            };

            if(!string.IsNullOrEmpty(doc.Get("DateBirthday")))
            {
                patient.DateBirthday = Convert.ToDateTime(doc.Get("DateBirthday"));
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
                writer.Dispose();
            }
        }

        private void InformarPath(string path)
        {
            _luceneDir = Path.Combine(HttpContext.Current.Request.PhysicalApplicationPath, "lucene_index");
        }

        private string TreatCharacters(IPatientDTO patient, List<string> hospital)
        {
            var str = "Name:";
            str += _removeSpecialCharacters(patient.Name);
            if (!string.IsNullOrEmpty(patient.DateBirthday.ToString()) && patient.DateBirthday.ToString() != "//")
            {
                str += " DateBirthday:";
                str += _removeSpecialCharacters(patient.DateBirthday.ToString());
            }

            var i = 1;
            foreach (var h in hospital)
            {
                if (hospital.Count > 1 && i < hospital.Count)
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

        private IEnumerable<IPatientDTO> _AdvancedSearch(IPatientDTO searchQuery, List<string> hospital)
        {
            var searchQueryStr = TreatCharacters(searchQuery, hospital);

            using (var searcher = new IndexSearcher(_directory, false))
            {

                var analyzer = new StandardAnalyzer(Version.LUCENE_30);

                string[] array = CreatParameters(searchQuery, hospital);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, array, analyzer);
                if (array.Count() > 1)
                    parser.DefaultOperator = QueryParser.Operator.AND;

                var query = parseQuery(searchQueryStr, parser);
                var hits = searcher.Search(query, null, 5000000, Sort.RELEVANCE).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();
                searcher.Dispose();

                return results;
            }
        }

        private string[] CreatParameters(IPatientDTO searchQuery, List<string> hospital)
        {
            var parameters = new List<string>();

            if (!string.IsNullOrEmpty(searchQuery.Name))
                parameters.Add("Name");

            if (!string.IsNullOrEmpty(searchQuery.DateBirthday.ToString()) && searchQuery.DateBirthday.ToString() != "//")
                parameters.Add("DateBirthday");

            if (hospital != null && hospital.Count > 0)
                parameters.Add("Hospital");

            return parameters.ToArray();
        }

        private void _addToLuceneIndex(IPatientDTO patient, IndexWriter writer)
        {
            RemoveIndex(patient, writer);
            var doc = new Document();
            AddFields(patient, doc);
            writer.AddDocument(doc);
        }

        private void AddFields(IPatientDTO patient, Document doc)
        {
            doc.Add(new Field("Id", patient.Id.ToString(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Name", patient.Name, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Hospital", patient.Hospital.ToString(), Field.Store.YES, Field.Index.ANALYZED));

            if (!string.IsNullOrEmpty(patient.CPF))
                doc.Add(new Field("CPF", patient.CPF, Field.Store.YES, Field.Index.ANALYZED));

            if (!string.IsNullOrEmpty(patient.DateBirthday.ToString()))
                doc.Add(new Field("DateBirthday", patient.DateBirthday.ToString(), Field.Store.YES, Field.Index.ANALYZED));
        }

        private void RemoveIndex(IPatientDTO patient, IndexWriter writer)
        {
            var searchQuery = new TermQuery(new Term("Id", patient.Id.ToString()));
            writer.DeleteDocuments(searchQuery);
        }
    }
}
