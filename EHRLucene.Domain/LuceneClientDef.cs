using EHR.CoreShared;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Version = Lucene.Net.Util.Version;


namespace EHRLucene.Domain
{
    public class LuceneClientDef
    {

        public LuceneClientDef(string path)
        {
            InformarPath(path);
            CriarDiretorio();

        }

        private void InformarPath(string path)
        {
            _luceneDir = Path.Combine(HttpContext.Current.Request.PhysicalApplicationPath, "lucene_index_Def");
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

        public void AddUpdateLuceneIndex(DEF patients)
        {
            AddUpdateLuceneIndex(new List<DEF> { patients });
            Optimize();
        }

        public void AddUpdateLuceneIndex(IEnumerable<DEF> sampleDatas)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var sampleData in sampleDatas) _addToLuceneIndex(sampleData, writer);

                analyzer.Close();
                writer.Dispose();
            }
        }

        private void _addToLuceneIndex(DEF def, IndexWriter writer)
        {
            //Não precisa remover o tratamento, pois existem varios tratamentos com o id igual.
            // RemoveIndex(treatment, writer);
            var doc = new Document();
            AddFields(def, doc);
            writer.AddDocument(doc);
        }

        private void AddFields(DEF def, Document doc)
        {
            doc.Add(new Field("Id", def.Id.ToString(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Description", def.Description.ToString().ToLower(), Field.Store.YES, Field.Index.ANALYZED));
        }

        private void RemoveIndex(DEF def, IndexWriter writer)
        {
            var searchQuery = new TermQuery(new Term("Id", def.Id.ToString()));
            writer.DeleteDocuments(searchQuery);
        }

        public IEnumerable<DEF> SimpleSearch(string input)
        {
            return _inputIsNotNullOrEmpty(input) ? new List<DEF>() : _SimpleSearch(input);

        }

        public IEnumerable<DEF> AdvancedSearch(List<DEF> defs)
        {
            return _AdvancedSearch(defs);
        }

        private string TreatCharacters(List<DEF> defs)
        {
            var str = "";

            var i = 1;
            foreach (var h in defs.Select(m => m.Id))
            {
                if (defs.Count > 1 && i < defs.Count)
                {
                    str += " Id:" + h.ToString() + " OR ";
                }
                else
                {
                    str += " Id:" + h.ToString();
                }
                i++;
            }

            return str;
        }

        private IEnumerable<DEF> _AdvancedSearch(List<DEF> defs)
        {
            var searchQueryStr = TreatCharacters(defs);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);

                string[] array = CreatParameters(defs);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, array, analyzer);
                parser.DefaultOperator = QueryParser.Operator.AND;

                var query = parseQuery(searchQueryStr, parser);
                var hits = searcher.Search(query, null, 5000000, Sort.RELEVANCE).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();
                searcher.Dispose();

                return results;
            }
        }

        private string[] CreatParameters(List<DEF> defs)
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


        private IEnumerable<DEF> _SimpleSearch(string searchQuery)
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

        private IEnumerable<DEF> _mapLuceneToDataList(IEnumerable<ScoreDoc> hits, IndexSearcher searcher)
        {
            return hits.Select(hit => _mapLuceneDocumentToData(searcher.Doc(hit.Doc))).ToList();
        }

        private DEF _mapLuceneDocumentToData(Document doc)
        {
            var def = new DEF()
            {
                Id = short.Parse(doc.Get("Id")),
                Description = doc.Get("Description"),
            };

            return def;
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

    }
}
