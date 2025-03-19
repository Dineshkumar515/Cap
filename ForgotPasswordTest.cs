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
using static ACQModel;
namespace TestAutomation.Tests.Account;
/// <summary>
/// Call POST ForgotPassword 
/// </summary>
/// <remarks>
/// Data Preconditions :
///
///
/// </remarks>
[TestClass]
public class ForgotPasswordTest
{
    private static readonly HttpClient HttpClient = new();
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
    
    private async Task Forgot_Password_Test_HappyPath(string emailAddress, string appName, CancellationToken cancellationToken)
    {
        ExtentReportManager.Log("Forgot Password Test Case Started...");

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
            string requestBody = Forgot_Password_Payload(emailAddress);

            // Construct the API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.ForgotPasswordApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);
                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 200 OK using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.OK, "Expected HTTP status code to be 200 OK.");

                    // Read the response body is empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

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
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137686011)]
    [TestCategory("Forgot_Password_Test_EmptyJsonBody")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4196")]
    public async Task ForgotPassword_EmptyJsonBody()
    {
        using var cts = new CancellationTokenSource();
        await Forgot_Password_Test_EmptyJsonBody(GlobalVariables.AppNameValue!, cts.Token);
    }
    private async Task Forgot_Password_Test_EmptyJsonBody( string appName, CancellationToken cancellationToken)
    {
        ExtentReportManager.Log("Forgot Password Empty Body Test Case Started...");

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
            string requestBody = string.Empty;

            // Construct the API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.ForgotPasswordApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);
                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 400 Bad Request using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "Expected HTTP status code to be 400 Bad Request.");

                    // Read the response body is empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeEmpty("Response body should not be empty.");

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
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137684281)]
    [TestCategory("Forgot_Password_Test_InvalidUrl")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4196")]
    public async Task Test_ForgotPassword_InvalidUrl()
    {
        using var cts = new CancellationTokenSource();
        string username = GenerateDynamicUsername();
        await Forgot_Password_Test_InvalidUrl(username, GlobalVariables.AppNameValue!, cts.Token);
    }

    /// <summary>
    /// Executes the test logic for an invalid URL scenario:
    /// 1. Generates a JWT token and sets up request headers.
    /// 2. Constructs an invalid API URL dynamically using input parameters.
    /// 3. Sends an API request and processes the response.
    /// 4. Logs results and validates response details.
    /// </summary>
    private async Task Forgot_Password_Test_InvalidUrl(string emailAddress, string appName, CancellationToken cancellationToken)
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
            string requestBody = Forgot_Password_Payload(emailAddress);

            // Construct an invalid API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}/invalid_endpoint";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);
                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 404 Not Found using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "Expected HTTP status code to be 404 Not Found.");

                    // Read the response body and ensure it is not empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should not be empty.");

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
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }
    [TestMethod]
    [TestRailCase(137684279)]
    [TestCategory("Forgot_Password_Test_InvalidEmail")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4196")]
    public async Task Test_ForgotPassword_InvalidEmail()
    {
        using var cts = new CancellationTokenSource();
        await Forgot_Password_Test_InvalidEmail("invalidemail", GlobalVariables.AppNameValue!, cts.Token);
    }

    /// <summary>
    /// Executes the test logic for an invalid email scenario:
    /// 1. Generates a JWT token and sets up request headers.
    /// 2. Constructs the API URL dynamically using input parameters.
    /// 3. Sends an API request and processes the response.
    /// 4. Logs results and validates response details.
    /// </summary>
    private async Task Forgot_Password_Test_InvalidEmail(string emailAddress, string appName, CancellationToken cancellationToken)
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
            string requestBody = Forgot_Password_Payload(emailAddress);

            // Construct the API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.ForgotPasswordApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);
                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 400 Bad Request using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "Expected HTTP status code to be 400 Bad Request.");

                    // Read the response body and ensure it is not empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrEmpty("Response body should not be null or empty.");

                    // Deserialize the JSON response
                    ErrorResponse? errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody);
                                        
                    // Assert the "code" and "message" values
                    errorResponse!.Code.ShouldBe("111041", "Error code should be 111041");
                    errorResponse.Message.ShouldBe("Invalid email address.", "Error message should match");
                    // Log the code and message
                    ExtentReportManager.Log($"Error Code: {errorResponse.Code}");
                    ExtentReportManager.Log($"Error Message: {errorResponse.Message}");
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
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137684280)]
    [TestCategory("Forgot_Password_Test_MissingEmail")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4196")]
    public async Task Test_ForgotPassword_MissingEmail()
    {
        using var cts = new CancellationTokenSource();
        await Forgot_Password_Test_MissingEmail(null, GlobalVariables.AppNameValue!, cts.Token);
    }

    /// <summary>
    /// Executes the test logic for a missing email scenario:
    /// 1. Generates a JWT token and sets up request headers.
    /// 2. Constructs the API URL dynamically using input parameters.
    /// 3. Sends an API request and processes the response.
    /// 4. Logs results and validates response details.
    /// </summary>
    private async Task Forgot_Password_Test_MissingEmail(string? emailAddress, string appName, CancellationToken cancellationToken)
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

            ExtentReportManager.Log($"Response Status: {emailAddress}");

            // Convert the updated JSON object to a string
            string requestBody = Forgot_Password_Payload(null);
            // Construct the API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.ForgotPasswordApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);
                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 400  using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "Expected HTTP status code to be 400 Bad Request.");

                    // Read the response body and ensure it is not empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrEmpty("Response body should not be null or empty.");
                    // Validate the response body content.
                    // Deserialize the JSON response
                    ErrorResponse? errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody);

                    // Assert the "code" and "message" values
                    errorResponse!.Code.ShouldBe("111008", "Error code should be 111008");
                    errorResponse.Message.ShouldBe("Please supply an email address", "Error message should match");
                    // Log the code and message
                    ExtentReportManager.Log($"Error Code: {errorResponse.Code}");
                    ExtentReportManager.Log($"Error Message: {errorResponse.Message}");
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
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137684272)]
    [TestCategory("Forgot_Password_Test_MissingJwt")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4196")]
    public async Task Test_ForgotPassword_MissingJwt()
    {
        using var cts = new CancellationTokenSource();
        string username = GenerateDynamicUsername();
        await Forgot_Password_Test_MissingJwt(username, GlobalVariables.AppNameValue!, cts.Token);
    }

    /// <summary>
    /// Executes the test logic for a missing JWT scenario:
    /// 1. Sets up request headers without JWT token.
    /// 2. Constructs the API URL dynamically using input parameters.
    /// 3. Sends an API request and processes the response.
    /// 4. Logs results and validates response details.
    /// </summary>
    private async Task Forgot_Password_Test_MissingJwt(string emailAddress, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Get other necessary headers (Correlation ID, OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = Forgot_Password_Payload(emailAddress);

            // Construct the API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.ForgotPasswordApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                // Missing JWT token
                // Custom headers required for the API.
                request.Headers.Add("x-api-key", xApiKey);
                request.Headers.Add("x-api-sig", oauthSig);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-Correlation-Id", correlationId);
                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 500 Internal Server Error using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");

                    // Read the response body is empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

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
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137684271)]
    [TestCategory("Forgot_Password_Test_InvalidJwt")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4196")]
    public async Task Test_ForgotPassword_InvalidJwt()
    {
        using var cts = new CancellationTokenSource();
        string username = GenerateDynamicUsername();
        await Forgot_Password_Test_InvalidJwt(username, GlobalVariables.AppNameValue!, cts.Token);
    }

    /// <summary>
    /// Executes the test logic for an invalid JWT scenario:
    /// 1. Generates an invalid JWT token and sets up request headers.
    /// 2. Constructs the API URL dynamically using input parameters.
    /// 3. Sends an API request and processes the response.
    /// 4. Logs results and validates response details.
    /// </summary>
    private async Task Forgot_Password_Test_InvalidJwt(string emailAddress, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Generate an invalid JWT token for API authentication.
            string invalidToken = "invalid.jwt.token";

            // Get other necessary headers (Correlation ID, OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = Forgot_Password_Payload(emailAddress);

            // Construct the API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.ForgotPasswordApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, invalidToken, correlationId, xApiKey, oauthSig);

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 401 Unauthorized using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "Expected HTTP status code to be 401 Unauthorized.");

                    // Read the response body and ensure it is not empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrEmpty("Response body should not be null or empty.");
                    //Response validation
                    responseBody.ShouldContain("<h1>Not Authorized</h1>");
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
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137684274)]
    [TestCategory("Forgot_Password_Test_MissingCorrelationId")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4196")]
    public async Task Test_ForgotPassword_MissingCorrelationId()
    {
        using var cts = new CancellationTokenSource();
        string username = GenerateDynamicUsername();
        await Forgot_Password_Test_MissingCorrelationId(username, GlobalVariables.AppNameValue!, cts.Token);
    }

    /// <summary>
    /// Executes the test logic for a missing Correlation ID scenario:
    /// 1. Generates a JWT token and sets up request headers without Correlation ID.
    /// 2. Constructs the API URL dynamically using input parameters.
    /// 3. Sends an API request and processes the response.
    /// 4. Logs results and validates response details.
    /// </summary>
    private async Task Forgot_Password_Test_MissingCorrelationId(string emailAddress, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Generate a JWT token for API authentication.
            var jwtTokenCreator = new JwtTokenGenerator(appName);
            string token = jwtTokenCreator.GenerateJwt();

            // Get other necessary headers (OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = Forgot_Password_Payload(emailAddress);

            // Construct the API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.ForgotPasswordApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, null, xApiKey, oauthSig); // Missing Correlation ID
                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 500 Internal Server Error using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");

                    // Read the response body is empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

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
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137684273)]
    [TestCategory("Forgot_Password_Test_InvalidCorrelationId")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4196")]
    public async Task Test_ForgotPassword_InvalidCorrelationId()
    {
        using var cts = new CancellationTokenSource();
        string username = GenerateDynamicUsername();
        await Forgot_Password_Test_InvalidCorrelationId(username, GlobalVariables.AppNameValue!, cts.Token);
    }

    /// <summary>
    /// Executes the test logic for an invalid Correlation ID scenario:
    /// 1. Generates a JWT token and sets up request headers with an invalid Correlation ID.
    /// 2. Constructs the API URL dynamically using input parameters.
    /// 3. Sends an API request and processes the response.
    /// 4. Logs results and validates response details.
    /// </summary>
    private async Task Forgot_Password_Test_InvalidCorrelationId(string emailAddress, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Generate a JWT token for API authentication.
            var jwtTokenCreator = new JwtTokenGenerator(appName);
            string token = jwtTokenCreator.GenerateJwt();

            // Get other necessary headers (OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = Forgot_Password_Payload(emailAddress);

            // Construct the API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.ForgotPasswordApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, "invalid-correlation-id", xApiKey, oauthSig); // Invalid Correlation ID

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 500 Internal Server Error using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");

                    // Read the response body is empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

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
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137684276)]
    [TestCategory("Forgot_Password_Test_MissingXApiKey")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4196")]
    public async Task Test_ForgotPassword_MissingXApiKey()
    {
        using var cts = new CancellationTokenSource();
        string username = GenerateDynamicUsername();
        await Forgot_Password_Test_MissingXApiKey(username, GlobalVariables.AppNameValue!, cts.Token);
    }

    /// <summary>
    /// Executes the test logic for a missing xApiKey scenario:
    /// 1. Generates a JWT token and sets up request headers without xApiKey.
    /// 2. Constructs the API URL dynamically using input parameters.
    /// 3. Sends an API request and processes the response.
    /// 4. Logs results and validates response details.
    /// </summary>
    private async Task Forgot_Password_Test_MissingXApiKey(string emailAddress, string appName, CancellationToken cancellationToken)
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

            // Convert the updated JSON object to a string
            string requestBody = Forgot_Password_Payload(emailAddress);

            // Construct the API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.ForgotPasswordApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, null, oauthSig); // Missing xApiKey
                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 500 Internal Server Error using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");

                    // Read the response body is empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

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
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137684275)]
    [TestCategory("Forgot_Password_Test_InvalidXApiKey")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4196")]
    public async Task Test_ForgotPassword_InvalidXApiKey()
    {
        using var cts = new CancellationTokenSource();
        string username = GenerateDynamicUsername();
        await Forgot_Password_Test_InvalidXApiKey(username, GlobalVariables.AppNameValue!, cts.Token);
    }

    /// <summary>
    /// Executes the test logic for an invalid xApiKey scenario:
    /// 1. Generates a JWT token and sets up request headers with an invalid xApiKey.
    /// 2. Constructs the API URL dynamically using input parameters.
    /// 3. Sends an API request and processes the response.
    /// 4. Logs results and validates response details.
    /// </summary>
    private async Task Forgot_Password_Test_InvalidXApiKey(string emailAddress, string appName, CancellationToken cancellationToken)
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
            string invalidXApiKey = "invalid-x-api-key";

            // Convert the updated JSON object to a string
            string requestBody = Forgot_Password_Payload(emailAddress);

            // Construct the API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.ForgotPasswordApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, invalidXApiKey, oauthSig); // Invalid xApiKey

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 401 Unauthorized using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "Expected HTTP status code to be 401 Unauthorized.");

                    // Read the response body and ensure it is not empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrEmpty("Response body should not be null or empty.");
                    //Response validation
                    responseBody.ShouldContain("<h1>Not Authorized</h1>");
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
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137684277)]
    [TestCategory("Forgot_Password_Test_InvalidOauthSig")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4196")]
    public async Task Test_ForgotPassword_InvalidOauthSig()
    {
        using var cts = new CancellationTokenSource();
        string username = GenerateDynamicUsername();
        await Forgot_Password_Test_InvalidOauthSig(username, GlobalVariables.AppNameValue!, cts.Token);
    }

    /// <summary>
    /// Executes the test logic for an invalid oauthSig scenario:
    /// 1. Generates a JWT token and sets up request headers with an invalid oauthSig.
    /// 2. Constructs the API URL dynamically using input parameters.
    /// 3. Sends an API request and processes the response.
    /// 4. Logs results and validates response details.
    /// </summary>
    private async Task Forgot_Password_Test_InvalidOauthSig(string emailAddress, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Generate a JWT token for API authentication.
            var jwtTokenCreator = new JwtTokenGenerator(appName);
            string token = jwtTokenCreator.GenerateJwt();

            // Get other necessary headers (Correlation ID).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");
            string invalidOauthSig = "invalid-oauth-sig";

            // Convert the updated JSON object to a string
            string requestBody = Forgot_Password_Payload(emailAddress);

            // Construct the API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.ForgotPasswordApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, invalidOauthSig); // Invalid oauthSig

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 500 Internal Server Error Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");

                    // Read the response body is empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

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
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137684278)]
    [TestCategory("Forgot_Password_Test_MissingOauthSig")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4196")]
    public async Task Test_ForgotPassword_MissingOauthSig()
    {
        using var cts = new CancellationTokenSource();
        string username = GenerateDynamicUsername();
        await Forgot_Password_Test_MissingOauthSig(username, GlobalVariables.AppNameValue!, cts.Token);
    }

    /// <summary>
    /// Executes the test logic for a missing oauthSig scenario:
    /// 1. Generates a JWT token and sets up request headers without oauthSig.
    /// 2. Constructs the API URL dynamically using input parameters.
    /// 3. Sends an API request and processes the response.
    /// 4. Logs results and validates response details.
    /// </summary>
    private async Task Forgot_Password_Test_MissingOauthSig(string emailAddress, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Generate a JWT token for API authentication.
            var jwtTokenCreator = new JwtTokenGenerator(appName);
            string token = jwtTokenCreator.GenerateJwt();

            // Get other necessary headers (Correlation ID).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = Forgot_Password_Payload(emailAddress);

            // Construct the API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.ForgotPasswordApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, null); // Missing oauthSig
                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 500 Internal Server Error Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");

                    // Read the response body is empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

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
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }
        
    [TestMethod]
    [TestRailCase(137684270)]
    [TestCategory("Forgot_Password_Test_HappyPath_CodeValidation")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4196")]
    public async Task Forgot_Password_HappyPath()
    {
        using var cts = new CancellationTokenSource();
        string username = GenerateDynamicUsername();
        await Create_Account_Test_HappyPath(
            username,
            "SbxPa#$w0rd", 
            "Test",
            "User",
            "US",
            "123 Any Street",
            "Apt. B",
            "Seattle",
            "WA",
            "US",
            "5555551212",
            5,
            4,
            "98001",
            "en-US",
            "Automation",
            "iOS",
            "10.75.23.112",
            "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
        await Forgot_Password_Test_HappyPath(username, GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task Create_Account_Test_HappyPath(
        string username,
     string password,
     string firstName,
     string lastName,
     string market,
     string addressLine1,
     string addressLine2,
     string city,
     string countrySubdivision,
     string country,
     string mobilePhoneNumber,
     int birthMonth,
     int birthDay,
     string postalCode,
     string preferredCulture,
     string registrationSource,
     string riskPlatform,
     string riskIpAddress,
     string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
    {
        ExtentReportManager.Log("Pre Condition CreateAccount Test Case Started...");
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
            string requestBody = CreateAccountPayload(
                username,
                password,
                firstName,
                lastName,
                market,
                addressLine1,
                addressLine2,
                city,
                countrySubdivision,
                country,
                mobilePhoneNumber,
                birthMonth,
                birthDay,
                postalCode,
                preferredCulture,
                registrationSource,
                riskPlatform,
                riskIpAddress,
                riskDeviceFingerprint);

            // Construct the API URL with query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);
                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {
                    // Assert the status code is 201 Created using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.Created, "Expected HTTP status code to be 201 Created.");

                    // Read the response body is empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldBeEmpty("Response body should be empty.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");
                    //Update Test Result in TestRail
                    int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
                    UpdateTestResult(testCaseId, result: "Test Passed", isResultString: true);
                    ExtentReportManager.Log("Pre Condition CreateAccount Test Case Ended...");
                }
            }
        }

        catch (Exception ex)
        {
            //Update Test Result in TestRail
            int testCaseId = TestRailTestCaseHelper.GetTestRailCaseId(TestContext!);
            UpdateTestResult(testCaseId, result: $"Test Failed : {ex}", isResultString: true);

            ExtentReportManager.Log($"Test failed: {ex.Message}");
            ExtentReportManager.LogFail("Test  failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    // Serilog log Logging Configuration
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
    /// <summary>
    /// Adds the required headers to the HTTP request.
    /// </summary>
    private void AddHeaders(HttpRequestMessage request, string? jwtToken, string? correlationId, string? xApiKey, string? oauthSig)
    {
        // Authorization header with Bearer token.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
        // Custom headers required for the API.
        request.Headers.Add("x-api-key", xApiKey);
        request.Headers.Add("x-api-sig", oauthSig);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Correlation-Id", correlationId);
    }

    // Create Forgot Password Payload Preparation Using Model 
    public static string Forgot_Password_Payload(string? emailAddress)
    {
        var requestData = new ForgotPasswordModel
        {
            EmailAddress = emailAddress

        };
        // Serialize the object to a JSON string
        string json = System.Text.Json.JsonSerializer.Serialize(requestData, new JsonSerializerOptions
        {
            WriteIndented = true // Makes the JSON output formatted
        });

        return json!;
    }

    // Create Account Payload Preparation Using Model
    public static string CreateAccountPayload(
     string username,
     string password,
     string firstName,
     string lastName,
     string market,
     string addressLine1,
     string addressLine2,
     string city,
     string countrySubdivision,
     string country,
     string mobilePhoneNumber,
     int birthMonth,
     int birthDay,
     string postalCode,
     string preferredCulture,
     string registrationSource,
     string riskPlatform,
     string riskIpAddress,
     string riskDeviceFingerprint)
    {
        // Build the payload with all required fields
        var requestData = new CreateAccountModel
        {
            Username = username,
            Password = password,
            FirstName = firstName,
            LastName = lastName,
            Market = market,
            AddressLine1 = addressLine1,
            AddressLine2 = addressLine2,
            City = city,
            CountrySubdivision = countrySubdivision,
            Country = country,
            MobilePhoneNumber = mobilePhoneNumber,
            BirthMonth = birthMonth,
            BirthDay = birthDay,
            PostalCode = postalCode,
            PreferredCulture = preferredCulture,
            RegistrationSource = registrationSource,
            Risk = new RiskModel
            {
                Platform = riskPlatform,
                Reputation = new ReputationModel
                {
                    IpAddress = riskIpAddress,
                    DeviceFingerprint = riskDeviceFingerprint
                }
            }
        };

        // Serialize the payload to a JSON string
        return System.Text.Json.JsonSerializer.Serialize(requestData, new JsonSerializerOptions
        {
            WriteIndented = true // Optional for debugging
        });
    }

    //Dynamic Username Generation
    public string GenerateDynamicUsername()
    {
        string dynamicUsername = $"testuser{DateTime.Now.Ticks}@sbuxautomation.com";
        return dynamicUsername;
    }

}
