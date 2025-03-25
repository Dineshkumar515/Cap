using Serilog;
using Shouldly;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using TestAutomation.CommonUtilities;
using TestAutomation.Helpers;
using TestAutomation.TestRailUtil;
using System.Collections.Concurrent;
using System.Net;
using TestAutomation.ApiModel;

[TestClass]
public class RegisterCardTest
{
    private static readonly HttpClient HttpClient = new();
    public TestContext? TestContext { get; set; }
    private static TestContext? classTestContext;
    private static readonly int TestSuiteRunId = Global.TestSuiteRunId;
    private static readonly ConcurrentDictionary<int, (string, int)> TestResultStore = new();
    private static readonly ThreadLocal<string> DynamicCardNumber = new();

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
        ExtentReportManager.StartTest(TestContext?.TestName ?? "DefaultTestName");
        Log.Information($"TestInitialize Start......{TestContext?.TestName}");
        int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
        TestResultStore[testCaseId] = ("Untested", 3);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        int statusId = TestContext?.CurrentTestOutcome == UnitTestOutcome.Passed ? 1 : 5;
        int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
        UpdateTestResult(testCaseId, statusId: statusId, isResultString: false);
        Log.Information($"Test Outcome: {TestContext?.CurrentTestOutcome}");
        Log.Information($"statusId : {statusId}");
        ExtentReportManager.EndTest();
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        List<TestRailModel.TestResults> res = [];
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
        if (Global.TestRailSwitch.Trim().Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            await TestRailClient.UpdateTestResultsAsync(TestSuiteRunId, res);
            Log.Information("TestRail Result Update done");
        }
        else
        {
            Log.Information($"TestRail Result Update Not Required Because TestRail Switch is :  {Global.TestRailSwitch}");
        }
        Log.Information("Tests Completed. Cleaning up...");
        Log.CloseAndFlush();
        ExtentReportManager.EndReport();
    }

    [TestMethod]
    [TestRailCase(137685854)]
    [TestCategory("UnRegister_Test_HappyPath")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4243")]

    public async Task Test_UnRegister_HappyPath()
    {
        using var cts = new CancellationTokenSource();
        await UnRegister_Test_HappyPath("IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task UnRegister_Test_HappyPath(string platform, string market, bool isLoggedIn, string ccAgentName, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Generate a JWT token for API authentication.
            var jwtTokenCreator = new JwtTokenGenerator(appName);
            string token = jwtTokenCreator.GenerateJwt();
            // Get other necessary headers (Correlation ID, OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");
            // Convert the updated JSON object to a string
            string requestBody = UnRegisterPayload(platform, market, isLoggedIn, ccAgentName);
            // Construct the API URL With query parameters.
            // string url = $"{GlobalVariables.BaseUrl}{{card_id}//unregister}";
            string cardId = "856773FA9AD318A29DF4";
            string url = $"{GlobalVariables.BaseUrl}/v1/me/cards/{cardId}/unregister";
            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);
                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 200 OK using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.OK, "Expected HTTP status code to be 200 Ok.");
                    // Read the response body and ensure it is empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should  be empty.");
                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    //Update Test Result in TestRail
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }

        catch (Exception ex)
        {
            //Update Test Result in TestRail
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true)
            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }


    [TestMethod]
    //(137686011)]
    [TestCategory("RegisterCard_Test_HappyPath_US")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4200")]
    public async Task Test_RegisterCard_HappyPath_US()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_HappyPath("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    
    [TestMethod]
    //(137686012)]
    [TestCategory("RegisterCard_Test_HappyPath_CAN")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4201")]
    public async Task Test_RegisterCard_HappyPath_CAN()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_HappyPath("7777475507830900", "01399227", "test", "CA", true, true, "web", "Teavana", "IOS", "CA", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_HappyPath(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.Created, "Expected HTTP status code to be 201 Created.");
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeEmpty("Response body should be empty.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    
    [TestMethod]
    //(137686013)]
    [TestCategory("RegisterCard_Test_InvalidUrl")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4202")]
    public async Task Test_RegisterCard_InvalidUrl()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_InvalidUrl("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_InvalidUrl(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = $"{GlobalVariables.BaseUrl}/invalid_url";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "Expected HTTP status code to be 404 Not Found.");
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    
    [TestMethod]
    //(137686014)]
    [TestCategory("RegisterCard_Test_MissingCorrelationId")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4203")]
    public async Task Test_RegisterCard_MissingCorrelationId()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_MissingCorrelationId("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_MissingCorrelationId(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-key", xApiKey);
                request.Headers.Add("x-api-sig", oauthSig);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Bad Request.");
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    //(137686015)]
    [TestCategory("RegisterCard_Test_InvalidCorrelationId")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4204")]
    public async Task Test_RegisterCard_InvalidCorrelationId()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_InvalidCorrelationId("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_InvalidCorrelationId(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            string correlationId = "invalid_correlation_id";
            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");


            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-key", xApiKey);
                request.Headers.Add("x-api-sig", oauthSig);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    //(137686016)]
    [TestCategory("RegisterCard_Test_EmptyCorrelationId")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4205")]
    public async Task Test_RegisterCard_EmptyCorrelationId()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_EmptyCorrelationId("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_EmptyCorrelationId(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            string correlationId = "";
            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");


            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-key", xApiKey);
                request.Headers.Add("x-api-sig", oauthSig);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server error.");
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

   
    [TestMethod]
    //(137686017)]
    [TestCategory("RegisterCard_Test_MissingXApiKey")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4206")]
    public async Task Test_RegisterCard_MissingXApiKey()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_MissingXApiKey("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_MissingXApiKey(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-sig", oauthSig);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    
    [TestMethod]
    //(137686018)]
    [TestCategory("RegisterCard_Test_InvalidXApiKey")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4207")]
    public async Task Test_RegisterCard_InvalidXApiKey()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_InvalidXApiKey("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_InvalidXApiKey(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = "invalid_x_api_key";

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-key", xApiKey);
                request.Headers.Add("x-api-sig", oauthSig);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "Expected HTTP status code to be 401 Unauthorized .");
                   
                    // Read the response body and ensure it is not empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    // Read the response body and ensure it is not empty.
                    responseBody.ShouldNotBeEmpty("Response body should not be empty.");
                    responseBody.ShouldContain("<h1>Not Authorized</h1>");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    
    [TestMethod]
    //(137686019)]
    [TestCategory("RegisterCard_Test_NoXApiKey")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4208")]
    public async Task Test_RegisterCard_EmptyXApiKey()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_EmptyXApiKey("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_EmptyXApiKey(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-sig", oauthSig);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    
    [TestMethod]
    //(137686017)]
    [TestCategory("RegisterCard_Test_MissingXApiSig")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4206")]
    public async Task Test_RegisterCard_MissingXApiSig()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_MissingXApiSig("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_MissingXApiSig(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-key", xApiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    
    [TestMethod]
    //(137686018)]
    [TestCategory("RegisterCard_Test_InvalidXApiSig")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4207")]
    public async Task Test_RegisterCard_InvalidXApiSig()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_InvalidXApiSig("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_InvalidXApiSig(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string oauthSig = "invalid_oauth_sig";
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-key", xApiKey);
                request.Headers.Add("x-api-sig", oauthSig);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

   
    [TestMethod]
    //(137686019)]
    [TestCategory("RegisterCard_Test_NoXApiSig")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4208")]
    public async Task Test_RegisterCard_EmptyXApiSig()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_EmptyXApiSig("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_EmptyXApiSig(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-key", xApiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    
    [TestMethod]
    //[//Case(137686020)]
    [TestCategory("RegisterCard_Test_MissingToken")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4209")]
    public async Task Test_RegisterCard_MissingToken()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_MissingToken("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_MissingToken(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Add("x-api-key", xApiKey);
                request.Headers.Add("x-api-sig", oauthSig);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

   
    [TestMethod]
    ////(137686021)]
    [TestCategory("RegisterCard_Test_InvalidToken")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4210")]
    public async Task Test_RegisterCard_InvalidToken()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_InvalidToken("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_InvalidToken(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            string token = "invalid_token";

            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-key", xApiKey);
                request.Headers.Add("x-api-sig", oauthSig);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "Expected HTTP status code to be 401 Unauthorized.");
                    // Read the response body and ensure it is not empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    // Read the response body and ensure it is not empty.
                    responseBody.ShouldNotBeEmpty("Response body should not be empty.");
                    responseBody.ShouldContain("<h1>Not Authorized</h1>");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

   
    [TestMethod]
    ////(122)]
    [TestCategory("RegisterCard_Test_Empty_Token")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4211")]
    public async Task Test_RegisterCard_Empty_Token()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_Empty_Token("7777475507809080", "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_Empty_Token(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            
            string token = "";
            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-key", xApiKey);
                request.Headers.Add("x-api-sig", oauthSig);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "Expected HTTP status code to be 401 Unauthorized.");
                    // Read the response body and ensure it is not empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    // Read the response body and ensure it is not empty.
                    responseBody.ShouldNotBeEmpty("Response body should not be empty.");
                    responseBody.ShouldContain("<h1>Not Authorized</h1>");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }


    [TestMethod]
    [TestCategory("RegisterCard_Test_Missing_Card_Number")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4215")]
    public async Task Test_RegisterCard_Missing_Card_Number()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_Missing_Card_Number(null, "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_Missing_Card_Number(string? cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "Expected HTTP status code to be 400 Bad Request.");
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                    // Deserialize the response body into a dictionary
                    Dictionary<string, string>? responseJson = null;
                    try
                    {
                        responseJson = JsonSerializer.Deserialize<Dictionary<string, string>>(responseBody);
                    }
                    catch (JsonException ex)
                    {
                        throw new Exception("Failed to deserialize the response body into a JSON dictionary.", ex);
                    }

                    // Ensure the response JSON is not null or empty
                    _ = responseJson.ShouldNotBeNull("The response JSON should not be null.");
                    responseJson.ShouldNotBeEmpty("The response JSON should contain at least one key-value pair.");

                    // Check the title field
                    _ = responseJson.TryGetValue("title", out string? title);
                    title.ShouldBe("Please supply a pin.", "The 'title' field should contain the expected error message.");
                    //Please supply a card number.

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }


    [TestMethod]
    [TestCategory("RegisterCard_Test_Missing_Pin_Number")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4215")]
    public async Task Test_RegisterCard_Missing_Pin_Number()
    {
        using var cts = new CancellationTokenSource();
        await RegisterCard_Test_Missing_Pin_Number("7777475507809080", null, "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_Missing_Pin_Number(string? cardNumber, string? pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            string url = "https://test.openapi.starbucks.com/v1/me/cards/register";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "Expected HTTP status code to be 400 Bad Request.");
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                    // Deserialize the response body into an ErrorResponse object
                    RegisterCardApiResponseModel? cardResponse = null;
                    try
                    {
                        cardResponse = JsonSerializer.Deserialize<RegisterCardApiResponseModel>(responseBody);
                    }
                    catch (JsonException ex)
                    {
                        throw new Exception("Failed to deserialize the response body into an ErrorResponse object.", ex);
                    }

                    // Ensure the error response is not null
                    _ = cardResponse.ShouldNotBeNull("The error response should not be null.");

                    // Check the title field
                    cardResponse.Title.ShouldBe("Please supply a pin.", "The 'title' field should contain the expected error message.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                }
            }
        }
        catch (Exception ex)
        {
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }
    private static void ConfigureLogger()
    {
        Serilog.Debugging.SelfLog.Enable(Console.WriteLine);
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string rootDir = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.FullName ?? string.Empty;

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

    private static void UpdateTestResult(int testCaseId, string? result = null, int? statusId = null, bool isResultString = false) =>
        TestResultStore.AddOrUpdate(testCaseId,
            key => (result ?? "", statusId ?? 0),
            (key, oldValue) =>
                (isResultString ? result ?? oldValue.Item1 : oldValue.Item1,
                 statusId ?? oldValue.Item2));

    private void AddHeaders(HttpRequestMessage request, string? jwtToken, string? correlationId, string? xApiKey, string? oauthSig)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
        request.Headers.Add("x-api-key", xApiKey);
        request.Headers.Add("x-api-sig", oauthSig);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Correlation-Id", correlationId);
       
    }

    public static string RegisterCardPayload(string? cardNumber, string? pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket, bool isLoggedIn, string ccAgentName)
    {
        var requestData = new
        {
            cardNumber,
            pin,
            nickname,
            submarket,
            primary,
            register,
            registrationSource = new
            {
                platform,
                marketing
            },
            risk = new
            {
                platform,
                market = riskMarket,
                isLoggedIn,
                ccAgentName
            }
        };
        return JsonSerializer.Serialize(requestData, new JsonSerializerOptions { WriteIndented = true });
    }

    // Un Register Payload Preparation Using Model
    public static string UnRegisterPayload(
        string platform,
        string market,
        bool isLoggedIn,
        string ccAgentName)
    {
        // Build the payload with all required fields
        var requestData = new CardModel.UnRegisterRisk
        {
            RiskModel = new CardModel.UnRegisterRiskModel
            {
                Platform = platform,
                Market = market,
                IsLoggedIn = isLoggedIn,
                CcAgentName = ccAgentName
            }
        };
    }
}
