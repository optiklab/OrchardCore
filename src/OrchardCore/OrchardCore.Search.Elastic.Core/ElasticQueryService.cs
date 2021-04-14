using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using Nest;
using Microsoft.Extensions.Logging;

namespace OrchardCore.Search.Elastic
{
    public class ElasticQueryService : IElasticQueryService
    {
        private readonly IEnumerable<IElasticQueryProvider> _queryProviders;
        private readonly IElasticClient _elasticClient;
        private readonly ILogger<ElasticQueryService> _logger;

        public ElasticQueryService(
            IEnumerable<IElasticQueryProvider> queryProviders,
            IElasticClient elasticClient,
            ILogger<ElasticQueryService> logger
            )
        {
            _queryProviders = queryProviders;
            _elasticClient = elasticClient;
            _logger = logger;
        }

        public async Task<ElasticTopDocs> SearchAsync(ElasticQueryContext context, JObject queryObj)
        {
            var queryProp = queryObj["query"] as JObject;

            if (queryProp == null)
            {
                throw new ArgumentException("Query DSL requires a [query] property");
            }

            ElasticTopDocs elasticTopDocs = new ElasticTopDocs();
            if (_elasticClient == null)
            {
                _logger.LogWarning("Elastic Client is not setup, please validate your Elastic Configurations");
            }

            try
            {
                var searchResponse = await _elasticClient.SearchAsync<ElasticDocument>(s
                    => s.Index(context.IndexName).Query(q => new RawQuery(queryProp.ToString())));
                if (searchResponse.IsValid)
                {
                    elasticTopDocs.Count = searchResponse.Documents.Count;
                    elasticTopDocs.TopDocs = searchResponse.Documents.ToList();
                }
                else
                {
                    _logger.LogError($"Received failure response from Elastic: { searchResponse.ServerError }");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while querying elastic with exception: { ex.Message}");
            }
            return elasticTopDocs;
        }

        /// <summary>
        /// May not be needed
        /// </summary>
        /// <param name="context"></param>
        /// <param name="queryObj"></param>
        /// <returns></returns>
        public Query CreateQueryFragment(ElasticQueryContext context, JObject queryObj)
        {
            var first = queryObj.Properties().First();

            Query query = null;

            foreach (var queryProvider in _queryProviders)
            {
                query = queryProvider.CreateQuery(this, context, first.Name, (JObject)first.Value);

                if (query != null)
                {
                    break;
                }
            }

            return query;
        }

        /// <summary>
        /// May not be neeeded
        /// </summary>
        /// <param name="fieldName"></param>
        /// <param name="text"></param>
        /// <param name="analyzer"></param>
        /// <returns></returns>
        public static List<string> Tokenize(string fieldName, string text, Analyzer analyzer)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<string>();
            }

            var result = new List<string>();
            using (var tokenStream = analyzer.GetTokenStream(fieldName, text))
            {
                tokenStream.Reset();
                while (tokenStream.IncrementToken())
                {
                    var termAttribute = tokenStream.GetAttribute<ICharTermAttribute>();

                    if (termAttribute != null)
                    {
                        result.Add(termAttribute.ToString());
                    }
                }
            }

            return result;
        }
    }
}