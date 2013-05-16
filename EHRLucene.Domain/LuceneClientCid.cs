using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using EHR.CoreShared;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Version = Lucene.Net.Util.Version;

namespace EHRLucene.Domain
{
    public class LuceneClientCid
    {
        public LuceneClientCid(string path)
        {
            InformarPath(path);
            CriarDiretorio();

        }

        private void InformarPath(string path)
        {
            _luceneDir = Path.Combine(HttpContext.Current.Request.PhysicalApplicationPath, "lucene_index_Cid");
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

        public void AddUpdateLuceneIndex(CidDTO cids)
        {
            AddUpdateLuceneIndex(new List<CidDTO> { cids });
            Optimize();
        }

        public void AddUpdateLuceneIndex(IEnumerable<CidDTO> sampleDatas)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var sampleData in sampleDatas) _addToLuceneIndex(sampleData, writer);

                analyzer.Close();
                writer.Dispose();
            }
        }

        private void _addToLuceneIndex(CidDTO cid, IndexWriter writer)
        {
            //Não precisa remover o tratamento, pois existem varios tratamentos com o id igual.
            // RemoveIndex(treatment, writer);
            var doc = new Document();
            AddFields(cid, doc);
            writer.AddDocument(doc);
        }

        private void AddFields(CidDTO cid, Document doc)
        {
            doc.Add(new Field("Id", cid.Id.ToString(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Description", cid.Description.ToString().ToLower(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Code", cid.Code.ToString().ToLower(), Field.Store.YES, Field.Index.ANALYZED));
        }

        private void RemoveIndex(CidDTO cid, IndexWriter writer)
        {
            var searchQuery = new TermQuery(new Term("Id", cid.Id.ToString()));
            writer.DeleteDocuments(searchQuery);
        }

        public IEnumerable<CidDTO> SimpleSearch(string input)
        {
            return _inputIsNotNullOrEmpty(input) ? new List<CidDTO>() : _SimpleSearch(input);

        }

        public IEnumerable<CidDTO> AdvancedSearch(List<CidDTO> cids)
        {
            return _AdvancedSearch(cids);
        }

        private string TreatCharacters(List<CidDTO> cids)
        {
            var str = "";

            var i = 1;
            foreach (var h in cids.Select(m => m.Id))
            {
                if (cids.Count > 1 && i < cids.Count)
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

        private IEnumerable<CidDTO> _AdvancedSearch(List<CidDTO> cids)
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
                searcher.Dispose();

                return results;
            }
        }

        private string[] CreatParameters(List<CidDTO> cids)
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


        private IEnumerable<CidDTO> _SimpleSearch(string searchQuery)
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

        private IEnumerable<CidDTO> _mapLuceneToDataList(IEnumerable<ScoreDoc> hits, IndexSearcher searcher)
        {
            return hits.Select(hit => _mapLuceneDocumentToData(searcher.Doc(hit.Doc))).ToList();
        }

        private CidDTO _mapLuceneDocumentToData(Document doc)
        {
            var cid = new CidDTO()
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
                writer.Dispose();
            }
        }
    }
}
