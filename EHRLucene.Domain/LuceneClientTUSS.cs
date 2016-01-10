using EHR.CoreShared.Entities;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using Version = Lucene.Net.Util.Version;
namespace EHRLucene.Domain
{
    public class LuceneClientTUSS
    {
        public string IndexDirectory;
        private FSDirectory _directoryTemp;
        private FSDirectory _directory
        {
            get
            {
                if (_directoryTemp == null) _directoryTemp = FSDirectory.Open(new DirectoryInfo(IndexDirectory));
                if (IndexWriter.IsLocked(_directoryTemp)) IndexWriter.Unlock(_directoryTemp);
                var lockFilePath = Path.Combine(IndexDirectory, "write.lock");
                return _directoryTemp;
            }
        }

        #region Constructors

        public LuceneClientTUSS(string path)
        {
            EntryPath(path);
            CreateDirectory();
        }

        #endregion

        #region Public Methods

        public void UpdateIndex(TUSS tuss)
        {
            UpdateIndex(new List<TUSS> { tuss });
            Optimize();
        }

        public void UpdateIndex(IEnumerable<TUSS> sampleDatas)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);

            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var sampleData in sampleDatas) AddToIndex(sampleData, writer);
                analyzer.Close();
            }
        }

        public IEnumerable<TUSS> SimpleSearch(string input)
        {
            return _inputIsNotNullOrEmpty(input) ? new List<TUSS>() : _SimpleSearch(input);

        }

        public IEnumerable<TUSS> AdvancedSearch(List<TUSS> tus)
        {
            return _AdvancedSearch(tus);
        }

        #endregion

        #region Private Methods

        private void CreateDirectory()
        {
            if (!System.IO.Directory.Exists(IndexDirectory)) System.IO.Directory.CreateDirectory(IndexDirectory);
        }

        private string TreatCharacters(List<TUSS> tus)
        {
            var str = "";

            var i = 1;
            foreach (var h in tus.Select(m => m.Id))
            {
                if (tus.Count > 1 && i < tus.Count)
                {
                    str += " Id:" + h.ToString(CultureInfo.InvariantCulture) + " OR ";
                }
                else
                {
                    str += " Id:" + h.ToString(CultureInfo.InvariantCulture);
                }
                i++;
            }

            return str;
        }

        private IEnumerable<TUSS> _AdvancedSearch(List<TUSS> tus)
        {
            var searchQueryStr = TreatCharacters(tus);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);

                string[] array = CreatParameters(tus);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, array, analyzer);
                parser.DefaultOperator = QueryParser.Operator.AND;

                var query = parseQuery(searchQueryStr, parser);
                var hits = searcher.Search(query, null, 5000000, Sort.RELEVANCE).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();

                return results;
            }
        }

        private string[] CreatParameters(List<TUSS> tus)
        {
            var parameters = new List<string>();

            parameters.Add("Id");
            return parameters.ToArray();
        }

        private MultiFieldQueryParser CreateParser(StandardAnalyzer analyzer)
        {
            var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Id" }, analyzer);
            parser.DefaultOperator = QueryParser.Operator.AND;
            return parser;
        }

        private IEnumerable<TUSS> _SimpleSearch(string searchQuery)
        {
            searchQuery = _removeSpecialCharacters(searchQuery);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Description" }, analyzer);
                var query = parseQuery(searchQuery, parser);
                var hits = searcher.Search(query, null, 10, new Sort(new SortField("Description", SortField.STRING))).ScoreDocs;
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

        private IEnumerable<TUSS> _mapLuceneToDataList(IEnumerable<ScoreDoc> hits, IndexSearcher searcher)
        {
            var tus = new List<TUSS>();

            foreach (var scoreDoc in hits)
            {
                tus.Add(_mapLuceneDocumentToData(searcher.Doc(scoreDoc.Doc)));
            }

            return tus;
        }

        private TUSS _mapLuceneDocumentToData(Document doc)
        {
            var tus = new TUSS
                          {
                              Id = short.Parse(doc.Get("Id")),
                              Description = doc.Get("Description"),
                              Code = doc.Get("Code"),
                          };

            return tus;
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

        private void EntryPath(string path)
        {
            if (string.IsNullOrEmpty(path) && HttpContext.Current != null)
            {
                if (HttpContext.Current.Request.PhysicalApplicationPath != null)
                    IndexDirectory = Path.Combine(HttpContext.Current.Request.PhysicalApplicationPath, "lucene_index_tus");

                return;
            }

            IndexDirectory = path;
        }

        private void AddToIndex(TUSS treatment, IndexWriter writer)
        {
            //Não precisa remover o tratamento, pois existem varios tratamentos com o id igual.
            // RemoveIndex(treatment, writer);
            var doc = new Document();
            AddFields(treatment, doc);
            writer.AddDocument(doc);
        }

        private void AddFields(TUSS tus, Document doc)
        {
            doc.Add(new Field("Id", tus.Id.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Description", tus.Description.ToLower(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Code", tus.Code.ToLower(), Field.Store.YES, Field.Index.ANALYZED));
        }

        private void RemoveIndex(TUSS tus, IndexWriter writer)
        {
            var searchQuery = new TermQuery(new Term("Id", tus.Id.ToString(CultureInfo.InvariantCulture)));
            writer.DeleteDocuments(searchQuery);
        }

        #endregion
    }
}
