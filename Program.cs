using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace ConsoleWebApp
{
    class Program
    {
        private const string LIST_ENDPOINT = @"https://appsheettest1.azurewebsites.net/sample/list";
        private const string DETAIL_ENDPOINT = @"https://appsheettest1.azurewebsites.net/sample/detail";
        static void Main(string[] args)
        {
            ListTopFiveYoungestUsers();
            Console.ReadKey();
        }

        /// <summary>
        /// Calls the specified endpoint
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="method"></param>
        /// <returns>returns JSON object of specified type</returns>
        static async Task<object> CallWebServiceAsync<T>(string url, string method) where T : class
        {
            var webReq = (HttpWebRequest)WebRequest.Create(url);
            webReq.Method = method;
            webReq.Accept = "application/json; charset=UTF-8";
            var jsonObject = default(T);
            WebResponse response = null;
            string responseValue = string.Empty;
            try
            {
                response = await webReq.GetResponseAsync();

                if (response == null)
                {
                    return default(T);
                }
                using (Stream responseStream = response.GetResponseStream())
                {
                    if (responseStream != null)
                    {
                        using (StreamReader reader = new StreamReader(responseStream))
                        {
                            responseValue = reader.ReadToEnd();
                        }
                    }
                }

                jsonObject = JsonConvert.DeserializeObject<T>(responseValue);
            }
            catch (Exception ex)
            {
                // catch non Http 200 responses here
                // log the error to some centralized log storage service
            }

            return jsonObject;
        }


        /// <summary>
        /// Returns the Person object for the specified user id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        static async Task<User> GetUserDetail(int id)
        {
            string endpoint = DETAIL_ENDPOINT + "/" + id.ToString();
            User user = await CallWebServiceAsync<User>(endpoint, "GET") as User;
            return user;
        }


        /// <summary>
        /// Matches US phone number format. 
        /// 1(country code) in the beginning is optional, area code is required, spaces, dashes, parantheses can be used as optional divider between number groups. 
        /// Also alphanumeric format is allowed after area code.
        /// Matches	1-(123)-123-1234 | 123 123 1234 | 1-800-ALPHNUM
        /// Non-Matches	1.123.123.1234 | (123)-1234-123 | 123-1234
        /// </summary>
        /// <param name="phoneNumber"></param>
        /// <returns>bool(true:match/false:no match)</returns>
        static bool IsValidUSPhoneNumber(string phoneNumber)
        {
            Regex regex = new Regex(@"^([0-9]( |-)?)?(\(?[0-9]{3}\)?|[0-9]{3})( |-)?([0-9]{3}( |-)?[0-9]{4}|[a-zA-Z0-9]{7})$");
            Match match = regex.Match(phoneNumber);
            return match.Success;
        }


        /// <summary>
        /// Prints top 5 youngest users with valid US telephone numbers sorted by name
        /// </summary>
        static async void ListTopFiveYoungestUsers()
        {
            string endpoint = LIST_ENDPOINT;
            var continuationToken = string.Empty;
            var resultList = new List<User>();

            do
            {
                try
                {
                    UserIDs parsedResponse = await CallWebServiceAsync<UserIDs>(endpoint, "GET") as UserIDs;
                    List<int> batchOfUsers = parsedResponse.result;
                    foreach (int id in batchOfUsers)
                    {
                        User user = await GetUserDetail(id);

                        if (user != null)
                        {
                            // only add Person objects with valid US phone numbers
                            if (IsValidUSPhoneNumber(user.number))
                            {
                                resultList.Add(user);
                            }
                        }
                    }
                    continuationToken = parsedResponse.token ?? string.Empty;
                    endpoint = LIST_ENDPOINT + "?token=" + continuationToken;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message.ToString());
                }
            } while (continuationToken.Length > 0);

            resultList = resultList.OrderBy(p => p.age)
                                 .Take(5)
                                 .OrderBy(p => p.name)
                                 .ToList();

            Console.WriteLine("{0,0}{1}{2}", "ID".ToString().PadRight(10), "Name".ToString().PadRight(15), "Age");
            Console.WriteLine("-----------------------------");
            resultList.ForEach(p => Console.WriteLine("{0}{1}{2}", p.id.ToString().PadRight(10), p.name.ToString().PadRight(15), p.age));
            Console.WriteLine("-----------------------------");
        }
    }
}
