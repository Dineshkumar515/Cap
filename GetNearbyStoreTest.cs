using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using Serilog;
using Shouldly;
using TestAutomation.CommonUtilities;
using TestAutomation.Helpers;
using TestAutomation.TestRailUtil;
using TestAutomation.ApiModel;
using Newtonsoft.Json;
using System.DirectoryServices.Protocols;

[TestClass]
public class GetNearbyStoresTest
{
    public TestContext? TestContext { get; set; }
    private static TestContext? classTestContext;
    private static readonly int TestSuiteRunId = Global.TestSuiteRunId;
    private static readonly ConcurrentDictionary<int, (string, int)> TestResultStore
       = new();


    [ClassInitialize]
    public static void InitClass(TestContext context)
    {
        ConfigureLogger();
        ExtentReportManager.StartReport();
        classTestContext = context;
        Log.Information($"ClassInitialize Start...... {classTestContext?.FullyQualifiedTestClassName}");
    }

    [TestInitialize]
    public void TestSetup()
    {
        // Ensure each test gets a unique instance in a parallel run
        ExtentReportManager.StartTest(TestContext?.TestName ?? "DefaultTestName");
        Log.Information($"TestInitialize Start......{TestContext?.TestName}");
        int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
        //Update Status as "Untested" before Test Run Start
        TestResultStore[testCaseId] = ("Untested", 3);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        int statusId;

        if (TestContext != null)
        {
            statusId = TestContext.CurrentTestOutcome == UnitTestOutcome.Passed ? 1 : 5;

            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);

            // Fill only the Test Result Status value
            UpdateTestResult(testCaseId, statusId: statusId, isResultString: false);

            Log.Information($"Test Outcome: {TestContext.CurrentTestOutcome}");
            Log.Information($"statusId : {statusId}");

        }
        else
        {
            Log.Warning("TestContext is null in TestCleanup.");
        }
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        List<TestRailModel.TestResults> res = [];

        try
        {
            foreach (KeyValuePair<int, (string, int)> kvp in TestResultStore)
            {
                int testCaseId = kvp.Key;
                int testId = await GetTestIdAsync(testCaseId);
                string comment = kvp.Value.Item1;
                int statusId = kvp.Value.Item2;

                Log.Information($"Test ID: {testCaseId}, Test Comment : {comment}, Test Result : {statusId}");

                res.Add(new TestRailModel.TestResults
                {
                    Test_id = testId,
                    Status_id = statusId,
                    Comment = comment
                });
            }

            //TestRail Status Update Switch
            if (Global.TestRailSwitch.Trim().Equals("on", StringComparison.OrdinalIgnoreCase))
            {
                //Update Entire result at a time
                await TestRailClient.UpdateTestResultsAsync(TestSuiteRunId, res);
                Log.Information("TestRail Result Update done");
            }
            else
            {
                Log.Information($"TestRail Result Update Not Required Because TestRail Switch is :  {Global.TestRailSwitch}");
            }
        }
        catch (Exception apiEx)
        {
            Log.Warning($"TestRail API Exception: {apiEx}");

        }

        Log.Information("Tests Completed. Cleaning up...");
        Log.CloseAndFlush();
        ExtentReportManager.EndReport();
    }

    [TestMethod]
    [TestRailCase(137680616)]
    [TestCategory("GetNearbyStores_happy_path_validationTest")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4187")]
    public async Task GetNearbyStores_e2e_happy_path_validation()
    {
        string appName = GlobalVariables.AppNameValue!;
        var logBuilder = new StringBuilder();
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token;

        try
        {
            ExtentReportManager.Log("Authenticating user...");
            string token = GenerateJwtToken(appName);

            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Calling Get Channel API  
            HttpResponseMessage? response = await GetNearbyStoresAPICallAsync(appName, cancellationToken);
            ExtentReportManager.Log("response : \n" + response);

            // Assert and log status codes
            int expectedStatusCode = 200;
            int actualStatusCode = (int)response.StatusCode;
            ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}, Actual Status Code: {actualStatusCode}");

            // Log the actual and expected status codes in the Extent Report
            ExtentReportManager.Log($"Actual Status Code: {actualStatusCode}");
            ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}");

            actualStatusCode.ShouldBe(expectedStatusCode);
            ExtentReportManager.LogPass("Test Passed");
            //Update Test Result in TestRail
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);

        }
        catch (Exception ex)
        {
            //Update Test Result in TestRail
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            // Log the exception details
            _ = logBuilder.AppendLine($"Test failed: {ex.Message}");
            _ = logBuilder.AppendLine("Test case Failed");
            ExtentReportManager.LogFail($"Test Failed: {ex.Message}"); // Log failure in Extent Report
            false.ShouldBeTrue($"Test failed due to exception: {ex.Message}");
        }
        finally
        {
            // Log the accumulated messages and end the test
            string logOutput = logBuilder.ToString();
            ExtentReportManager.Log(logOutput); // Log to the reporting tool
        }
    }

    public static async Task<HttpResponseMessage> GetNearbyStoresAPICallAsync(string appName, CancellationToken cancelToken)
    {
        HttpResponseMessage response;
        string baseUrl = $"{GlobalVariables.BaseUrl}";
        string clientId = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");
        string clientSecret = GlobalVariables.ClientSecret ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientSecret), "Client Secret cannot be null.");
        string username = GlobalVariables.UserName ?? throw new ArgumentNullException(nameof(GlobalVariables.UserName), "Username cannot be null.");
        string password = GlobalVariables.Password ?? throw new ArgumentNullException(nameof(GlobalVariables.Password), "Password cannot be null.");

        // Generate OAuth token for API authentication.
        string token = await OAuthTokenGenerator.GetOAuthTokenAsync(baseUrl, clientId, clientSecret, username, password, cancelToken);

        // Generating OauthToken and CorrelationId
        var acObj = new OauthTokenAndCorrelationId(appName);
        string correlationId = acObj.CorrelationId;
        string oauthSig = acObj.OauthSig;
        int limit = 50;
        double Latitude = 45.49408;
        double Longitude = -122.76729;

        string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.GetNearbyStoresPathComponent}{limit}&latlng={Latitude},{Longitude}&roles=Starbucks API - Trusted";
        string? xApiKey = GlobalVariables.ClientId;

        HttpClient client = GetHttpClient(new Uri(baseUrl));
        {
            // Setup headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("x-api-key", xApiKey);
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
            client.DefaultRequestHeaders.Add("x-api-sig", oauthSig);

            // Setup the request
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                // Make the API Request
                response = await client.SendAsync(request, cancelToken);
            }
        }

        return response;
    }

    [TestMethod]
    [TestRailCase(137680621)]
    [TestCategory("GetNearbyStores_invalid_jwt_token")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4187")]
    public async Task GetNearbyStores_invalid_jwt_token()
    {
        string appName = GlobalVariables.AppNameValue!;
        var logBuilder = new StringBuilder();
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token;

        try
        {
            ExtentReportManager.Log("Authenticating user with invalid token...");
            string token = "invalid_token"; // Invalid token

            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            HttpResponseMessage? response = await GetNearbyStores_Invalid_JWT_Token_Async(appName, token, correlationId, oauthSig, xApiKey, cancellationToken);
            ExtentReportManager.Log("response : \n" + response);

            // Assert and log status codes
            int expectedStatusCode = 401;
            int actualStatusCode = (int)response.StatusCode;
            ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}, Actual Status Code: {actualStatusCode}");

            // Log the actual and expected status codes in the Extent Report
            ExtentReportManager.Log($"Actual Status Code: {actualStatusCode}");
            ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}");

            actualStatusCode.ShouldBe(expectedStatusCode);
            ExtentReportManager.LogPass("Test Passed");
            //Update Test Result in TestRail
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);

        }
        catch (Exception ex)
        {
            //Update Test Result in TestRail
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            // Log the exception details
            _ = logBuilder.AppendLine($"Test failed: {ex.Message}");
            _ = logBuilder.AppendLine("Test case Failed");
            ExtentReportManager.LogFail($"Test Failed: {ex.Message}"); // Log failure in Extent Report
            false.ShouldBeTrue($"Test failed due to exception: {ex.Message}");
        }
        finally
        {
            // Log the accumulated messages and end the test
            string logOutput = logBuilder.ToString();
            ExtentReportManager.Log(logOutput); // Log to the reporting tool
        }
    }

    [TestMethod]
    [TestRailCase(137680620)]
    [TestCategory("GetNearbyStores_missing_jwt_token_Test")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4187")]
    public async Task GetNearbyStores_missing_jwt_token()
    {
        string appName = GlobalVariables.AppNameValue!;
        var logBuilder = new StringBuilder();
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token;

        try
        {
            ExtentReportManager.Log("Authenticating user with missing token...");


            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            HttpResponseMessage? response = await GetNearbyStores_Missing_JWT_Token_Async(appName, correlationId, oauthSig, xApiKey, cancellationToken);
            ExtentReportManager.Log("response : \n" + response);

            // Assert and log status codes
            int expectedStatusCode = 401;
            int actualStatusCode = (int)response.StatusCode;
            ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}, Actual Status Code: {actualStatusCode}");

            // Log the actual and expected status codes in the Extent Report
            ExtentReportManager.Log($"Actual Status Code: {actualStatusCode}");
            ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}");

            actualStatusCode.ShouldBe(expectedStatusCode);
            ExtentReportManager.LogPass("Test Passed");
            //Update Test Result in TestRail
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);

        }
        catch (Exception ex)
        {
            //Update Test Result in TestRail
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            // Log the exception details
            _ = logBuilder.AppendLine($"Test failed: {ex.Message}");
            _ = logBuilder.AppendLine("Test case Failed");
            ExtentReportManager.LogFail($"Test Failed: {ex.Message}"); // Log failure in Extent Report
            false.ShouldBeTrue($"Test failed due to exception: {ex.Message}");
        }
        finally
        {
            // Log the accumulated messages and end the test
            string logOutput = logBuilder.ToString();
            ExtentReportManager.Log(logOutput); // Log to the reporting tool
        }
    }

    [TestMethod]
    [TestRailCase(137680619)]
    [TestCategory("GetNearbyStores_invalid_api_endpoint_Test")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4187")]
    public async Task GetNearbyStores_invalid_api_endpoint()
    {
        string appName = GlobalVariables.AppNameValue!;
        var logBuilder = new StringBuilder();
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token;

        try
        {
            string baseUrl = $"{GlobalVariables.BaseUrl}";
            string clientId = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");
            string clientSecret = GlobalVariables.ClientSecret ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientSecret), "Client Secret cannot be null.");
            string username = GlobalVariables.UserName ?? throw new ArgumentNullException(nameof(GlobalVariables.UserName), "Username cannot be null.");
            string password = GlobalVariables.Password ?? throw new ArgumentNullException(nameof(GlobalVariables.Password), "Password cannot be null.");

            // Generating OauthToken and CorrelationId
            var acObj = new OauthTokenAndCorrelationId(appName);
            //string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;

            // Generate OAuth token for API authentication.
            string token = GenerateJwtToken(appName);
            //var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            // string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            string invalidUrl = $"{GlobalVariables.BaseUrl}/v1/"; // Invalid endpoint
            HttpResponseMessage? response = await GetNearbyStores_API_InValid_EndPointCall_Async(appName, token, correlationId, oauthSig, xApiKey, cancellationToken);
            ExtentReportManager.Log("response : \n" + response);

            // Assert and log status codes
            int expectedStatusCode = 404;
            int actualStatusCode = (int)response.StatusCode;
            ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}, Actual Status Code: {actualStatusCode}");

            // Log the actual and expected status codes in the Extent Report
            ExtentReportManager.Log($"Actual Status Code: {actualStatusCode}");
            ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}");

            actualStatusCode.ShouldBe(expectedStatusCode);
            ExtentReportManager.LogPass("Test Passed");
            //Update Test Result in TestRail
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);

        }
        catch (Exception ex)
        {
            //Update Test Result in TestRail
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            // Log the exception details
            _ = logBuilder.AppendLine($"Test failed: {ex.Message}");
            _ = logBuilder.AppendLine("Test case Failed");
            ExtentReportManager.LogFail($"Test Failed: {ex.Message}"); // Log failure in Extent Report
            false.ShouldBeTrue($"Test failed due to exception: {ex.Message}");
        }
        finally
        {
            // Log the accumulated messages and end the test
            string logOutput = logBuilder.ToString();
            ExtentReportManager.Log(logOutput); // Log to the reporting tool
        }
    }

    [TestMethod]
    [TestRailCase(137680622)]
    [TestCategory("GetNearbyStores_missing_x_api_key_header_Test")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4187")]
    public async Task GetNearbyStores_missing_x_api_key_header()
    {
        int expectedStatusCode = 401;
        var logBuilder = new StringBuilder();
        string appName = GlobalVariables.AppNameValue!;

        using (CancellationTokenSource cts = new())
        {
            CancellationToken cancellationToken = cts.Token;
            try
            {
                HttpResponseMessage? GetNearbyStoresResponse = await GetNearbyStores_Missing_XAPIKeyApiCallAsync(appName, cancellationToken);

                ExtentReportManager.Log($"Response Message: {GetNearbyStoresResponse}");
                int actualStatusCode = (int)GetNearbyStoresResponse.StatusCode;

                // Log the actual and expected status codes in the Extent Report
                ExtentReportManager.Log($"Actual Status Code: {actualStatusCode}");
                ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}");

                actualStatusCode.ShouldBe(expectedStatusCode);
                ExtentReportManager.LogPass("Test Passed");
                //Update Test Result in TestRail
                int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
            }
            catch (Exception ex)
            {
                //Update Test Result in TestRail
                int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

                // Log the exception details
                _ = logBuilder.AppendLine($"Test failed: {ex.Message}");
                _ = logBuilder.AppendLine("Test case Failed");
                ExtentReportManager.LogFail($"Test Failed: {ex.Message}"); // Log failure in Extent Report
                false.ShouldBeTrue($"Test failed due to exception: {ex.Message}");
            }
            finally
            {
                // Log the accumulated messages and end the test
                string logOutput = logBuilder.ToString();
                ExtentReportManager.Log(logOutput); // Log to the reporting tool
            }
        }
    }

    [TestMethod]
    [TestRailCase(137680623)]
    [TestCategory("GetNearbyStores_missing_x_api_sig_header_Test")]
    [TestCategory("P0")]
    [TestCategory(  "DPCE-4187")]

    public async Task GetNearbyStores_missing_x_api_sig_header()
    {
        int expectedStatusCode = 401;
        var logBuilder = new StringBuilder();
        string appName = GlobalVariables.AppNameValue!;

        using (CancellationTokenSource cts = new())
        {
            CancellationToken cancellationToken = cts.Token;
            try
            {
                HttpResponseMessage? GetNearbyStoresResponse = await GetNearbyStores_Missing_XAPISigApiCallAsync(appName, cancellationToken);

                ExtentReportManager.Log($"Response Message: {GetNearbyStoresResponse}");
                int actualStatusCode = (int)GetNearbyStoresResponse.StatusCode;

                // Log the actual and expected status codes in the Extent Report
                ExtentReportManager.Log($"Actual Status Code: {actualStatusCode}");
                ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}");

                actualStatusCode.ShouldBe(expectedStatusCode);
                ExtentReportManager.LogPass("Test Passed");
                //Update Test Result in TestRail
                int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);

            }
            catch (Exception ex)
            {
                //Update Test Result in TestRail
                int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

                // Log the exception details
                _ = logBuilder.AppendLine($"Test failed: {ex.Message}");
                _ = logBuilder.AppendLine("Test case Failed");
                ExtentReportManager.LogFail($"Test Failed: {ex.Message}"); // Log failure in Extent Report
                false.ShouldBeTrue($"Test failed due to exception: {ex.Message}");
            }
            finally
            {
                // Log the accumulated messages and end the test
                string logOutput = logBuilder.ToString();
                ExtentReportManager.Log(logOutput); // Log to the reporting tool
            }
        }
    }

    [TestMethod]
    [TestRailCase(137680624)]
    [TestCategory("GetNearbyStores_InValid_x_api_key_header_Test")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4187")]
    public async Task GetNearbyStores_InValid_x_api_key_header()
    {
        int expectedStatusCode = 401;
        var logBuilder = new StringBuilder();
        string appName = GlobalVariables.AppNameValue!;

        using (CancellationTokenSource cts = new())
        {
            CancellationToken cancellationToken = cts.Token;
            try
            {
                HttpResponseMessage? GetNearbyStoresResponse = await GetNearbyStores_Invalid_XAPIKeyApiCallAsync(appName, cancellationToken);

                ExtentReportManager.Log($"Response Message: {GetNearbyStoresResponse}");
                int actualStatusCode = (int)GetNearbyStoresResponse.StatusCode;

                // Log the actual and expected status codes in the Extent Report
                ExtentReportManager.Log($"Actual Status Code: {actualStatusCode}");
                ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}");

                actualStatusCode.ShouldBe(expectedStatusCode);
                ExtentReportManager.LogPass("Test Passed");
                //Update Test Result in TestRail
                int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
            }
            catch (Exception ex)
            {
                //Update Test Result in TestRail
                int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

                // Log the exception details
                _ = logBuilder.AppendLine($"Test failed: {ex.Message}");
                _ = logBuilder.AppendLine("Test case Failed");
                ExtentReportManager.LogFail($"Test Failed: {ex.Message}"); // Log failure in Extent Report
                false.ShouldBeTrue($"Test failed due to exception: {ex.Message}");
            }
            finally
            {
                // Log the accumulated messages and end the test
                string logOutput = logBuilder.ToString();
                ExtentReportManager.Log(logOutput); // Log to the reporting tool
            }
        }
    }

    [TestMethod]
    [TestRailCase(137680625)]
    [TestCategory("GetNearbyStores_InValid_x_api_sig_header_Test")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4187")]

    public async Task GetNearbyStores_InValid_x_api_sig_header()
    {
        int expectedStatusCode = 401;
        var logBuilder = new StringBuilder();
        string appName = GlobalVariables.AppNameValue!;

        using (CancellationTokenSource cts = new())
        {
            CancellationToken cancellationToken = cts.Token;
            try
            {
                HttpResponseMessage? GetNearbyStoresResponse = await GetNearbyStores_Invalid_XAPISigApiCallAsync(appName, cancellationToken);

                ExtentReportManager.Log($"Response Message: {GetNearbyStoresResponse}");
                int actualStatusCode = (int)GetNearbyStoresResponse.StatusCode;

                // Log the actual and expected status codes in the Extent Report
                ExtentReportManager.Log($"Actual Status Code: {actualStatusCode}");
                ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}");

                actualStatusCode.ShouldBe(expectedStatusCode);
                ExtentReportManager.LogPass("Test Passed");
                //Update Test Result in TestRail
                int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);

            }
            catch (Exception ex)
            {
                //Update Test Result in TestRail
                int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

                // Log the exception details
                _ = logBuilder.AppendLine($"Test failed: {ex.Message}");
                _ = logBuilder.AppendLine("Test case Failed");
                ExtentReportManager.LogFail($"Test Failed: {ex.Message}"); // Log failure in Extent Report
                false.ShouldBeTrue($"Test failed due to exception: {ex.Message}");
            }
            finally
            {
                // Log the accumulated messages and end the test
                string logOutput = logBuilder.ToString();
                ExtentReportManager.Log(logOutput); // Log to the reporting tool
            }
        }
    }

    [TestMethod]
    [TestRailCase(137680617)]
    [TestCategory("GetNearbyStores_Invalid_Latitude_Test")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4187")]

    public async Task GetNearbyStores_Invalid_Latitude()
    {
        int expectedStatusCode = 400;
        var logBuilder = new StringBuilder();
        string appName = GlobalVariables.AppNameValue!;

        using (CancellationTokenSource cts = new())
        {
            CancellationToken cancellationToken = cts.Token;
            try
            {

                HttpResponseMessage? GetNearbyStoresResponse = await GetNearbyStores_Invalid_Latitude_CallAsync(appName, cancellationToken);

                ExtentReportManager.Log($"Response Message: {GetNearbyStoresResponse}");
                int actualStatusCode = (int)GetNearbyStoresResponse.StatusCode;

                // Log the actual and expected status codes in the Extent Report
                ExtentReportManager.Log($"Actual Status Code: {actualStatusCode}");
                ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}");

                actualStatusCode.ShouldBe(expectedStatusCode);
                ExtentReportManager.LogPass("Test Passed");
                //Update Test Result in TestRail
                int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);

            }
            catch (Exception ex)
            {
                //Update Test Result in TestRail
                int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

                // Log the exception details
                _ = logBuilder.AppendLine($"Test failed: {ex.Message}");
                _ = logBuilder.AppendLine("Test case Failed");
                ExtentReportManager.LogFail($"Test Failed: {ex.Message}"); // Log failure in Extent Report
                false.ShouldBeTrue($"Test failed due to exception: {ex.Message}");
            }
            finally
            {
                // Log the accumulated messages and end the test
                string logOutput = logBuilder.ToString();
                ExtentReportManager.Log(logOutput); // Log to the reporting tool
            }
        }
    }

    [TestMethod]
    [TestRailCase(137680618)]
    [TestCategory("GetNearbyStores_Invalid_Longitude_Test")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4187")]
    public async Task GetNearbyStores_Invalid_Longitude()
    {
        int expectedStatusCode = 400;
        var logBuilder = new StringBuilder();
        string appName = GlobalVariables.AppNameValue!;

        using (CancellationTokenSource cts = new())
        {
            CancellationToken cancellationToken = cts.Token;
            try
            {
                HttpResponseMessage? GetNearbyStoresResponse = await GetNearbyStores_Invalid_Longitude_CallAsync(this.baseUrl, appName, cancellationToken);

                ExtentReportManager.Log($"Response Message: {GetNearbyStoresResponse}");
                int actualStatusCode = (int)GetNearbyStoresResponse.StatusCode;

                // Log the actual and expected status codes in the Extent Report
                ExtentReportManager.Log($"Actual Status Code: {actualStatusCode}");
                ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}");

                actualStatusCode.ShouldBe(expectedStatusCode);
                ExtentReportManager.LogPass("Test Passed");
                //Update Test Result in TestRail
                int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
            }
            catch (Exception ex)
            {
                //Update Test Result in TestRail
                int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

                // Log the exception details
                _ = logBuilder.AppendLine($"Test failed: {ex.Message}");
                _ = logBuilder.AppendLine("Test case Failed");
                ExtentReportManager.LogFail($"Test Failed: {ex.Message}"); // Log failure in Extent Report
                false.ShouldBeTrue($"Test failed due to exception: {ex.Message}");
            }
            finally
            {
                // Log the accumulated messages and end the test
                string logOutput = logBuilder.ToString();
                ExtentReportManager.Log(logOutput); // Log to the reporting tool
            }
        }
    }

    [TestMethod]
    [TestRailCase(137681296)]
    [TestCategory("GetNearbyStores_Response_Validation_Test")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4187")]

    public async Task GetNearbyStores_Response_Validation()
    {
        string appName = GlobalVariables.AppNameValue!;
        var logBuilder = new StringBuilder();
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token;

        try
        {
            ExtentReportManager.Log("Authenticating user...");
            string token = GenerateJwtToken(appName);

            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Calling Get Channel API  
            HttpResponseMessage? response = await GetNearbyStores_Response_Validation_CallAsync(appName, cancellationToken);
            ExtentReportManager.Log("response : \n" + response);

            // Assert and log status codes
            int expectedStatusCode = 200;
            int actualStatusCode = (int)response.StatusCode;
            ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}, Actual Status Code: {actualStatusCode}");

            // Validate response content
            string responseBody = await response.Content.ReadAsStringAsync();
            ValidateResponseContent(responseBody);
            ExtentReportManager.LogPass("Response content validation passed");

            // Log the actual and expected status codes in the Extent Report
            ExtentReportManager.Log($"Actual Status Code: {actualStatusCode}");
            ExtentReportManager.Log($"Expected Status Code: {expectedStatusCode}");

            actualStatusCode.ShouldBe(expectedStatusCode);
            ExtentReportManager.LogPass("Test Passed");
            //Update Test Result in TestRail
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);

        }
        catch (Exception ex)
        {
            //Update Test Result in TestRail
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            // Log the exception details
            _ = logBuilder.AppendLine($"Test failed: {ex.Message}");
            _ = logBuilder.AppendLine("Test case Failed");
            ExtentReportManager.LogFail($"Test Failed: {ex.Message}"); // Log failure in Extent Report
            false.ShouldBeTrue($"Test failed due to exception: {ex.Message}");
        }
        finally
        {
            // Log the accumulated messages and end the test
            string logOutput = logBuilder.ToString();
            ExtentReportManager.Log(logOutput); // Log to the reporting tool
        }
    }

    public static async Task<HttpResponseMessage> GetNearbyStores_Missing_XAPIKeyApiCallAsync(string appName, CancellationToken cancelToken)
    {
        HttpResponseMessage response;
        string token = GenerateJwtToken(appName);
        // Generating OauthToken and CorrelationId
        var acObj = new OauthTokenAndCorrelationId(appName);
        string correlationId = acObj.CorrelationId;
        string oauthSig = acObj.OauthSig;
        string baseUrl = $"{GlobalVariables.BaseUrl}";
        int limit = 50;
        double Latitude = 45.49408;
        double Longitude = -122.76729;

        string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.GetNearbyStoresPathComponent}{limit}&latlng={Latitude},{Longitude}&roles=Starbucks API - Trusted";
        HttpClient client = GetHttpClient(new Uri(baseUrl));
        {
            // Setup headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
            client.DefaultRequestHeaders.Add("x-api-sig", oauthSig);

            // Setup the request

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                // Make the API Request
                response = await client.SendAsync(request, cancelToken);
            }
        }

        return response;
    }
    public static async Task<HttpResponseMessage> GetNearbyStores_Invalid_XAPIKeyApiCallAsync(string appName, CancellationToken cancelToken)
    {
        HttpResponseMessage response;
        string token = GenerateJwtToken(appName);
        // Generating OauthToken and CorrelationId
        var acObj = new OauthTokenAndCorrelationId(appName);
        string correlationId = acObj.CorrelationId;
        string oauthSig = acObj.OauthSig;
        string baseUrl = $"{GlobalVariables.BaseUrl}";
        int limit = 50;
        double Latitude = 45.49408;
        double Longitude = -122.76729;

        string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.GetNearbyStoresPathComponent}{limit}&latlng={Latitude},{Longitude}&roles=Starbucks API - Trusted";


        HttpClient client = GetHttpClient(new Uri(baseUrl));
        {
            // Setup headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
            client.DefaultRequestHeaders.Add("x-api-sig", oauthSig);
            client.DefaultRequestHeaders.Add("x-api-key", "In Valid API Key");
            // Setup the request
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                // Make the API Request
                response = await client.SendAsync(request, cancelToken);
            }
        }

        return response;
    }
    public static async Task<HttpResponseMessage> GetNearbyStores_Missing_XAPISigApiCallAsync(string appName, CancellationToken cancelToken)
    {
        HttpResponseMessage response;

        string token = GenerateJwtToken(appName);
        // Generating OauthToken and CorrelationId
        var acObj = new OauthTokenAndCorrelationId(appName);
        string correlationId = acObj.CorrelationId;
        string baseUrl = $"{GlobalVariables.BaseUrl}";
        int limit = 50;
        double Latitude = 45.49408;
        double Longitude = -122.76729;

        string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.GetNearbyStoresPathComponent}{limit}&latlng={Latitude},{Longitude}&roles=Starbucks API - Trusted";

        string? xApiKey = GlobalVariables.ClientId;

        HttpClient client = GetHttpClient(new Uri(baseUrl));
        {
            // Setup headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("x-api-key", xApiKey);
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);

            // Setup the request
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                // Make the API Request
                response = await client.SendAsync(request, cancelToken);
            }
        }

        return response;
    }
    public static async Task<HttpResponseMessage> GetNearbyStores_Invalid_XAPISigApiCallAsync(string appName, CancellationToken cancelToken)
    {
        HttpResponseMessage response;

        string token = GenerateJwtToken(appName);
        // Generating OauthToken and CorrelationId
        var acObj = new OauthTokenAndCorrelationId(appName);
        string correlationId = acObj.CorrelationId;
        string baseUrl = $"{GlobalVariables.BaseUrl}";
        int limit = 50;
        double Latitude = 45.49408;
        double Longitude = -122.76729;

        string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.GetNearbyStoresPathComponent}{limit}&latlng={Latitude},{Longitude}&roles=Starbucks API - Trusted";

        string? xApiKey = GlobalVariables.ClientId;

        HttpClient client = GetHttpClient(new Uri(baseUrl));
        {
            // Setup headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("x-api-key", xApiKey);
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
            client.DefaultRequestHeaders.Add("x-api-sig", "Invalid OAUth Sig");
            // Setup the request
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                // Make the API Request
                response = await client.SendAsync(request, cancelToken);
            }
        }

        return response;
    }
    public static async Task<HttpResponseMessage> GetNearbyStores_Invalid_JWT_Token_Async(string appName, string token, string correlationId, string oauthSig, string? xApiKey, CancellationToken cancelToken)
    {
        HttpResponseMessage response;
        string baseUrl = $"{GlobalVariables.BaseUrl}";
        int limit = 50;
        double Latitude = 45.49408;
        double Longitude = -122.76729;

        string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.GetNearbyStoresPathComponent}{limit}&latlng={Latitude},{Longitude}&roles=Starbucks API - Trusted";

        HttpClient client = GetHttpClient(new Uri(baseUrl));
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (xApiKey != null)
            {
                client.DefaultRequestHeaders.Add("x-api-key", xApiKey);
            }
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
            client.DefaultRequestHeaders.Add("x-api-sig", oauthSig);

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                response = await client.SendAsync(request, cancelToken);
            }
        }

        return response;
    }

    public static async Task<HttpResponseMessage> GetNearbyStores_Missing_JWT_Token_Async(string appName, string correlationId, string oauthSig, string? xApiKey, CancellationToken cancelToken)
    {
        HttpResponseMessage response;
        string baseUrl = $"{GlobalVariables.BaseUrl}";
        int limit = 50;
        double Latitude = 45.49408;
        double Longitude = -122.76729;

        string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.GetNearbyStoresPathComponent}{limit}&latlng={Latitude},{Longitude}&roles=Starbucks API - Trusted";

        HttpClient client = GetHttpClient(new Uri(baseUrl));
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (xApiKey != null)
            {
                client.DefaultRequestHeaders.Add("x-api-key", xApiKey);
            }
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
            client.DefaultRequestHeaders.Add("x-api-sig", oauthSig);

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                response = await client.SendAsync(request, cancelToken);
            }
        }

        return response;
    }

    public static async Task<HttpResponseMessage> GetNearbyStores_API_InValid_EndPointCall_Async(string appName, string token, string correlationId, string oauthSig, string? xApiKey, CancellationToken cancelToken)
    {
        HttpResponseMessage response;
        string baseUrl = $"{GlobalVariables.BaseUrl}";
        string url = $"{GlobalVariables.BaseUrl}/v1/";

        HttpClient client = GetHttpClient(new Uri(baseUrl));
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (xApiKey != null)
            {
                client.DefaultRequestHeaders.Add("x-api-key", xApiKey);
            }
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
            client.DefaultRequestHeaders.Add("x-api-sig", oauthSig);

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                response = await client.SendAsync(request, cancelToken);
            }
        }

        return response;
    }

    public static async Task<HttpResponseMessage> GetNearbyStores_Invalid_Latitude_CallAsync(string appName, CancellationToken cancelToken)
    {
        HttpResponseMessage response;
        string token = GenerateJwtToken(appName);
        // Generating OauthToken and CorrelationId
        var acObj = new OauthTokenAndCorrelationId(appName);
        string correlationId = acObj.CorrelationId;
        string oauthSig = acObj.OauthSig;
        string baseUrl = $"{GlobalVariables.BaseUrl}";
        int limit = 50;
        double Longitude = -122.76729;

        string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.GetNearbyStoresPathComponent}{limit}&latlng=45.494o8,{Longitude}&roles=Starbucks API - Trusted";
        
        HttpClient client = GetHttpClient(new Uri(baseUrl));
        {
            // Setup headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
            client.DefaultRequestHeaders.Add("x-api-sig", oauthSig);

            // Setup the request
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                // Make the API Request
                response = await client.SendAsync(request, cancelToken);
            }
        }

        return response;
    }

    public static async Task<HttpResponseMessage> GetNearbyStores_Invalid_Longitude_CallAsync(string baseUrl, string appName, CancellationToken cancelToken)
    {
        HttpResponseMessage response;
        string token = GenerateJwtToken(appName);
        // Generating OauthToken and CorrelationId
        var acObj = new OauthTokenAndCorrelationId(appName);
        string correlationId = acObj.CorrelationId;
        string oauthSig = acObj.OauthSig;
        int limit = 50;
        double Latitude = 45.49408;

        string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.GetNearbyStoresPathComponent}{limit}&latlng={Latitude},-122.7i6729&roles=Starbucks API - Trusted";
        HttpClient client = GetHttpClient(new Uri(baseUrl));
        {
            // Setup headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
            client.DefaultRequestHeaders.Add("x-api-sig", oauthSig);

            // Setup the request
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                // Make the API Request
                response = await client.SendAsync(request, cancelToken);
            }
        }

        return response;
    }

   
    public static async Task<HttpResponseMessage> GetNearbyStores_Response_Validation_CallAsync(string appName, CancellationToken cancelToken)
    {
        HttpResponseMessage response;
        string baseUrl = $"{GlobalVariables.BaseUrl}";
        string clientId = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");
        string clientSecret = GlobalVariables.ClientSecret ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientSecret), "Client Secret cannot be null.");
        string username = GlobalVariables.UserName ?? throw new ArgumentNullException(nameof(GlobalVariables.UserName), "Username cannot be null.");
        string password = GlobalVariables.Password ?? throw new ArgumentNullException(nameof(GlobalVariables.Password), "Password cannot be null.");

        // Generate OAuth token for API authentication.
        string token = await OAuthTokenGenerator.GetOAuthTokenAsync(baseUrl, clientId, clientSecret, username, password, cancelToken);

        // Generating OauthToken and CorrelationId
        var acObj = new OauthTokenAndCorrelationId(appName);
        string correlationId = acObj.CorrelationId;
        string oauthSig = acObj.OauthSig;
        int limit = 50;
        double Latitude = 45.49408;
        double Longitude = -122.76729;

        string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.GetNearbyStoresPathComponent}{limit}&latlng={Latitude},{Longitude}&roles=Starbucks API - Trusted";
        string? xApiKey = GlobalVariables.ClientId;

        HttpClient client = GetHttpClient(new Uri(baseUrl));
        {
            // Setup headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("x-api-key", xApiKey);
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
            client.DefaultRequestHeaders.Add("x-api-sig", oauthSig);

            // Setup the request
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                // Make the API Request
                response = await client.SendAsync(request, cancelToken);
            }
        }

        return response;
    }

    private void ValidateResponseContent(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            throw new ArgumentException("Response body should not be empty.");
        }

        // Deserialize the response JSON into the ApiResponse model.
        NearbyStoresApiResponseValidationModel? apiResponse = JsonConvert.DeserializeObject<NearbyStoresApiResponseValidationModel>(responseBody);
        if (apiResponse == null || apiResponse.Stores == null)
        {
            throw new Exception("Response data should not be null.");
        }

        foreach (NearbyStoresApiResponseValidationModel store in stores)
        {
            // Validate each property in the response.
            _ = store.ShouldNotBeNull("Response data should not be null.");
            _ = store.StoreNumber.ShouldBeOfType<int?>("Store number should be of type int?");
            _ = store.Id.ShouldBeOfType<int?>("ID should be of type int?");
            _ = store.Name.ShouldBeOfType<string>("Name should be of type string.");
            _ = store.BrandName.ShouldBeOfType<string>("BrandName should be of type string.");
            _ = store.PhoneNumber.ShouldBeOfType<string>("Phone number should be of type string.");
            _ = store.DistrictId.ShouldBeOfType<int?>("District Id should be of type int?");
            _ = store.OwnershipTypeCode.ShouldBeOfType<string>("Ownership type code should be of type string.");
            _ = store.Market.ShouldBeOfType<string>("Market should be of type string.");

            bool allFieldsValid = store.StoreNumber.HasValue &&
                                  store.Id.HasValue &&
                                  !string.IsNullOrWhiteSpace(store.Name) &&
                                  !string.IsNullOrWhiteSpace(store.BrandName) &&
                                  !string.IsNullOrWhiteSpace(store.PhoneNumber) &&
                                  store.DistrictId.HasValue &&
                                  !string.IsNullOrWhiteSpace(store.OwnershipTypeCode) &&
                                  !string.IsNullOrWhiteSpace(store.Market);

            if (!allFieldsValid)
            {
                throw new Exception("One or more required fields are missing or empty in the response.");
            }
        }
    }


    //Serilog log Logging Configuration
    private static void ConfigureLogger()
    {
        // Enable Serilog debugging
        Serilog.Debugging.SelfLog.Enable(Console.WriteLine);

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string rootDir = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.FullName ?? string.Empty;

        // Set up log directory and file path
        string logDirectory = Path.Combine(rootDir, "logs");
        if (!Directory.Exists(logDirectory))
        {
            _ = Directory.CreateDirectory(logDirectory);
        }

        string logFilePath = Path.Combine(logDirectory, "test-log.txt");
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, shared: true)
            .CreateLogger();

        Log.Information("Serilog configured successfully.");
        Log.Information($"Logs will be written to: {logFilePath}");
    }
    private static async Task<int> GetTestIdAsync(int testCaseId)
    {
        try
        {
            List<TestRailModel.TestCase> tests = await TestRailClient.GetTestCasesAsync(TestSuiteRunId).ConfigureAwait(false);
            TestRailModel.TestCase? test = tests.Find(t => t.Case_id == testCaseId);
            return test?.Id ?? 0;
        }
        catch (Exception ex)
        {
            Log.Warning($"Error fetching Test ID: {ex}");
            return 0;
        }
    }

    private static void UpdateTestResult(int testCaseId, string? result = null, int? statusId = null, bool isResultString = false) => TestResultStore.AddOrUpdate(testCaseId,
            key => (result ?? "", statusId ?? 0), // Initial insert
            (key, oldValue) =>
                (isResultString ? result ?? oldValue.Item1 : oldValue.Item1,
                 statusId ?? oldValue.Item2));

    private static string GenerateJwtToken(string appName)
    {
        var jwtTokenCreator = new JwtTokenGenerator(appName);
        return jwtTokenCreator.GenerateJwt();
    }

    public static HttpClient GetHttpClient(Uri baseAddress)
    {
        if (HttpClientCache.TryGetValue(baseAddress, out HttpClient? cachedClient))
        {
            return cachedClient;
        }

        HttpClient httpClient = new()
        {
            BaseAddress = baseAddress,
        };

        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(AcceptJson));

        HttpClientCache[baseAddress] = httpClient;
        return httpClient;
    }

    private readonly string baseUrl = $"{GlobalVariables.BaseUrl}";
    // Static HttpClient Cache
    private static readonly ConcurrentDictionary<Uri, HttpClient> HttpClientCache = new();
    private const string AcceptJson = "application/json";
}
