using EHR.CoreShared.Entities;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using Version = Lucene.Net.Util.Version;

namespace EHRLucene.Domain
{
    public class LuceneClientCID
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

        public LuceneClientCID(string path)
        {
            EntryPath(path);
            CreateDirectory();
        }

        #endregion

        #region Public Methods

        public void UpdateIndex(CID cids)
        {
            UpdateIndex(new List<CID> { cids });
            Optimize();
        }

        public void UpdateIndex(IEnumerable<CID> sampleDatas)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var sampleData in sampleDatas) _addToLuceneIndex(sampleData, writer);

                analyzer.Close();
            }
        }

        public IEnumerable<CID> AdvancedSearch(List<CID> cids)
        {
            return _AdvancedSearch(cids);
        }

        public IEnumerable<CID> SimpleSearch(string input)
        {
            return _inputIsNotNullOrEmpty(input) ? new List<CID>() : _SimpleSearch(input);

        }

        #endregion

        #region Private Methods

        private void EntryPath(string path)
        {
            if (HttpContext.Current != null)
            {
                if (HttpContext.Current.Request.PhysicalApplicationPath != null)
                {
                    IndexDirectory = Path.Combine(HttpContext.Current.Request.PhysicalApplicationPath, "lucene_index_cid");
                }
                else if (string.IsNullOrEmpty(path) && string.IsNullOrEmpty(IndexDirectory))
                {
                    IndexDirectory = "C:\\lucene_index_cid";
                }
                else
                {
                    IndexDirectory = path;
                }
            }
            else if (string.IsNullOrEmpty(path) && string.IsNullOrEmpty(IndexDirectory))
            {
                IndexDirectory = "C:\\lucene_index_cid";
            }
            else
            {
                IndexDirectory = path;
            }
        }

        private void CreateDirectory()
        {
            if (!System.IO.Directory.Exists(IndexDirectory)) System.IO.Directory.CreateDirectory(IndexDirectory);
        }

        private void _addToLuceneIndex(CID cid, IndexWriter writer)
        {
            //Não precisa remover o tratamento, pois existem varios tratamentos com o id igual.
            // RemoveIndex(treatment, writer);
            var doc = new Document();
            AddFields(cid, doc);
            writer.AddDocument(doc);
        }

        private void AddFields(CID cid, Document doc)
        {
            doc.Add(new Field("Id", cid.Id.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Description", cid.Description.ToLower(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Code", cid.Code.ToLower(), Field.Store.YES, Field.Index.ANALYZED));
        }

        private void RemoveIndex(CID cid, IndexWriter writer)
        {
            var searchQuery = new TermQuery(new Term("Id", cid.Id.ToString(CultureInfo.InvariantCulture)));
            writer.DeleteDocuments(searchQuery);
        }

        private string TreatCharacters(List<CID> cids)
        {
            var str = "";

            var i = 1;
            foreach (var h in cids.Select(m => m.Id))
            {
                if (cids.Count > 1 && i < cids.Count)
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

        private IEnumerable<CID> _AdvancedSearch(List<CID> cids)
        {
            var searchQueryStr = TreatCharacters(cids);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);

                string[] array = CreatParameters(cids);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, array, analyzer);
                parser.DefaultOperator = QueryParser.Operator.AND;

                var query = parseQuery(searchQueryStr, parser);
                var hits = searcher.Search(query, null, 5000000, Sort.RELEVANCE).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();

                return results;
            }
        }

        private string[] CreatParameters(List<CID> cids)
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

        private IEnumerable<CID> _SimpleSearch(string searchQuery)
        {
            searchQuery = _removeSpecialCharacters(searchQuery);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Description" }, analyzer);
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

        private IEnumerable<CID> _mapLuceneToDataList(IEnumerable<ScoreDoc> hits, IndexSearcher searcher)
        {
            return hits.Select(hit => _mapLuceneDocumentToData(searcher.Doc(hit.Doc))).ToList();
        }

        private CID _mapLuceneDocumentToData(Document doc)
        {
            var cid = new CID
                          {
                              Id = short.Parse(doc.Get("Id")),
                              Description = doc.Get("Description"),
                              Code = doc.Get("Code"),
                          };

            return cid;
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

        #endregion
    }
}
