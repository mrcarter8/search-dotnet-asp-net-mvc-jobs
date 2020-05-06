using Azure.Search;
using Azure.Search.Models;
using Microsoft.Spatial;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Web;

namespace NYCJobsWeb
{
    public class JobsSearch
    {
        private static SearchServiceClient _searchClient;
        private static SearchIndexClient _indexClient;
        private static string IndexName = "nycjobs";
        private static SearchIndexClient _indexZipClient;
        private static string IndexZipCodes = "zipcodes";

        public static string errorMessage;

        static JobsSearch()
        {
            try
            {
                string searchServiceName = ConfigurationSettings.AppSettings["SearchServiceName"];
                string apiKey = ConfigurationSettings.AppSettings["SearchServiceApiKey"];

                // Create an HTTP reference to the catalog index
                var serviceUri = new Uri(String.Format("https://{0}.search.windows.net", searchServiceName));
                _searchClient = new SearchServiceClient(serviceUri, new SearchApiKeyCredential(apiKey));
                _indexClient = new SearchIndexClient(serviceUri, IndexName, new SearchApiKeyCredential(apiKey)); 
                _indexZipClient = new SearchIndexClient(serviceUri, IndexZipCodes, new SearchApiKeyCredential(apiKey));

            }
            catch (Exception e)
            {
                errorMessage = e.Message.ToString();
            }
        }

        public SearchResults<SearchDocument> Search(string searchText, string businessTitleFacet, string postingTypeFacet, string salaryRangeFacet,
            string sortType, double lat, double lon, int currentPage, int maxDistance, string maxDistanceLat, string maxDistanceLon)
        {
            // Execute search based on query string
            try
            {
                SearchOptions so = new SearchOptions()
                {
                    SearchMode = SearchMode.Any,
                    Size = 10,
                    Skip = currentPage - 1,
                    // Add count
                    IncludeTotalCount = true,

                    HighlightPreTag = "<b>",
                    HighlightPostTag = "</b>",
 
                };
                // Limit results
                foreach (var item in new List<String>() {"id", "agency", "posting_type", "num_of_positions", "business_title",
                        "salary_range_from", "salary_range_to", "salary_frequency", "work_location", "job_description",
                        "posting_date", "geo_location", "tags"})
                {
                    so.Select.Add(item);
                }
                // Add search highlights
                so.HighlightFields.Add("job_description");
                // Add facets
                foreach (var item in new List<String>() { "business_title", "posting_type", "level", "salary_range_from,interval:50000" })
                {
                    so.Facets.Add(item);
                }
                // Define the sort type
                if (sortType == "featured")
                {
                    so.ScoringProfile = "jobsScoringFeatured";      // Use a scoring profile
                    so.ScoringParameters.Add("featuredParam-featured");
                    so.ScoringParameters.Add("mapCenterParam-" + lon + "," + lat);
                }
                else if (sortType == "salaryDesc")
                    so.OrderBy.Add("salary_range_from desc");
                else if (sortType == "salaryIncr")
                    so.OrderBy.Add("salary_range_from");
                else if (sortType == "mostRecent")
                    so.OrderBy.Add("posting_date desc");

                // Add filtering
                string filter = null;
                if (businessTitleFacet != "")
                    filter = "business_title eq '" + businessTitleFacet + "'";
                if (postingTypeFacet != "")
                {
                    if (filter != null)
                        filter += " and ";
                    filter += "posting_type eq '" + postingTypeFacet + "'";

                }
                if (salaryRangeFacet != "")
                {
                    if (filter != null)
                        filter += " and ";
                    filter += "salary_range_from ge " + salaryRangeFacet + " and salary_range_from lt " + (Convert.ToInt32(salaryRangeFacet) + 50000).ToString();
                }

                if (maxDistance > 0)
                {
                    if (filter != null)
                        filter += " and ";
                    filter += "geo.distance(geo_location, geography'POINT(" + maxDistanceLon + " " + maxDistanceLat + ")') le " + maxDistance.ToString();
                }

                so.Filter = filter;

                return _indexClient.Search(searchText, so).Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error querying index: {0}\r\n", ex.Message.ToString());
            }
            return null;
        }

        public SearchResults<SearchDocument> SearchZip(string zipCode)
        {
            // Execute search based on query string
            try
            {
                SearchOptions sp = new SearchOptions()
                {
                    SearchMode = SearchMode.All,
                    Size = 1
                };
                return _indexZipClient.Search(zipCode, sp).Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error querying index: {0}\r\n", ex.Message.ToString());
            }
            return null;
        }

        public SuggestResults<SearchDocument> Suggest(string searchText, bool fuzzy)
        {
            // Execute search based on query string
            try
            {
                SuggestOptions so = new SuggestOptions()
                {
                    UseFuzzyMatching = fuzzy,
                    Size = 8
                };

                return _indexClient.Suggest(searchText, "sg", so).Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error querying index: {0}\r\n", ex.Message.ToString());
            }
            return null;
        }

        public SearchDocument LookUp(string id)
        {
            // Execute geo search based on query string
            try
            {
                return _indexClient.GetDocument(id);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error querying index: {0}\r\n", ex.Message.ToString());
            }
            return null;
        }

    }
}