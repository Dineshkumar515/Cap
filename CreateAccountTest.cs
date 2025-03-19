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
/// <summary>
/// Call POST Create Account
/// </summary>
[TestClass]
public class CreateAccountTest
{
    private static readonly HttpClient HttpClient = new();
    public TestContext? TestContext { get; set; }
    private static TestContext? classTestContext;
    private static readonly int TestSuiteRunId = Global.TestSuiteRunId;
    private static readonly ConcurrentDictionary<int, (string, int)> TestResultStore
       = new();
    private static readonly ThreadLocal<string> DynamicUsername = new();

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
            ExtentReportManager.EndTest();
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
    [TestRailCase(137685854)]
    [TestCategory("CreateAccount_Test_HappyPath")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_HappyPath()
    {
        using var cts = new CancellationTokenSource();
        string username = $"testuser{DateTime.Now.Ticks}@sbuxautomation.com"; // Dynamic username
        DynamicUsername.Value = username;
        await CreateAccount_Test_HappyPath(username, "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_HappyPath(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
        string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
        string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
                string requestBody = CreateAccountPayload(
                username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
                preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
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

                    // Read the response body and ensure it is empty.
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
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137685855)]
    [TestCategory("CreateAccount_Test_InvalidEndPoint")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_InvalidEndPoint()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_InvalidEndPoint("testuset7s45583@sbuxautomation.com", "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_InvalidEndPoint(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
        string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
        string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}/retail/customers/v2/create";

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

                    // Read the response body and ensure it is empty.
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
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137685856)]
    [TestCategory("CreateAccount_Test_MissingJWT")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_MissingJWT()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_MissingJWT("testuset7s45583@sbuxautomation.com", "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_MissingJWT(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
       string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
       string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Get other necessary headers (Correlation ID, OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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

                    // Assert the status code is 500 InternalServerError using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 InternalServerError.");

                    // Read the response body and ensure it is empty.
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
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137685857)]
    [TestCategory("CreateAccount_Test_InvalidJWT")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_InvalidJWT()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_InvalidJWT("testuset7s45583@sbuxautomation.com", "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_InvalidJWT(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
       string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
       string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
    {
        try
        {
            //invalid JWT token
            string token = "1212";

            // Get other necessary headers (Correlation ID, OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);

                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {

                    // Assert the status code is 401 Unauthorized using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "Expected HTTP status code to be 401 Unauthorized.");

                    // Read the response body and ensure it is not empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeEmpty("Response body should not be empty.");
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
    [TestRailCase(137685858)]
    [TestCategory("CreateAccount_Test_With_Empty_CorrelationId")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_With_Empty_CorrelationId()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_With_Empty_CorrelationId("testuset7s45583@sbuxautomation.com", "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_With_Empty_CorrelationId(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
    string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
    string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Generate a JWT token for API authentication.
            var jwtTokenCreator = new JwtTokenGenerator(appName);
            string token = jwtTokenCreator.GenerateJwt();

            // Get other necessary headers (Correlation ID, OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = "";
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);

                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {

                    // Assert the status code is 500 Internal Server Error using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");

                    // Read the response body and ensure it is empty.
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
    [TestRailCase(137685859)]
    [TestCategory("CreateAccount_Test_InvalidCorrelationId")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_InvalidCorrelationId()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_InvalidCorrelationId("testuset7s45583@sbuxautomation.com", "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_InvalidCorrelationId(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
        string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
        string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Generate a JWT token for API authentication.
            var jwtTokenCreator = new JwtTokenGenerator(appName);
            string token = jwtTokenCreator.GenerateJwt();

            // Get other necessary headers (Correlation ID, OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = "1234";
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);

                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {

                    // Assert the status code is 500 InternalServerError using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 InternalServerError.");

                    // Read the response body and ensure it is empty.
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
    [TestRailCase(137685860)]
    [TestCategory("CreateAccount_Test_With_Empty_XApiKey")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_With_Empty_XApiKey()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_With_Empty_XApiKey("testuset7s45583@sbuxautomation.com", "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_With_Empty_XApiKey(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
        string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
        string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string xApiKey = "";

            // Convert the updated JSON object to a string
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);

                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {

                    // Assert the status code is 500 InternalServerError using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 InternalServerError.");

                    // Read the response body and ensure it is empty.
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
    [TestRailCase(137685861)]
    [TestCategory("CreateAccount_Test_InvalidXApiKey")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_InvalidXApiKey()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_InvalidXApiKey("testuset7s45583@sbuxautomation.com", "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_InvalidXApiKey(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
        string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
        string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string xApiKey = "123";

            // Convert the updated JSON object to a string
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);

                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {

                    // Assert the status code is 401 Unauthorized using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "Expected HTTP status code to be 401 Unauthorized.");

                    // Read the response body and ensure it is not empty.
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeEmpty("Response body should not be empty.");
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
    [TestRailCase(137685862)]
    [TestCategory("CreateAccount_Test_InvalidOauthSig")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_InvalidOauthSig()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_InvalidOauthSig("testuset7s45583@sbuxautomation.com", "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_InvalidOauthSig(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
        string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
        string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Generate a JWT token for API authentication.
            var jwtTokenCreator = new JwtTokenGenerator(appName);
            string token = jwtTokenCreator.GenerateJwt();

            // Get other necessary headers (Correlation ID, OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string oauthSig = "1234";
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);

                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {

                    // Assert the status code is 500 InternalServerError using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 InternalServerError.");

                    // Read the response body and ensure it is empty.
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
    [TestRailCase(137685863)]
    [TestCategory("CreateAccount_Test_With_Empty_OauthSig")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_With_Empty_OauthSig()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_With_Empty_OauthSig("testuset7s45583@sbuxautomation.com", "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_With_Empty_OauthSig(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
        string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
        string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Generate a JWT token for API authentication.
            var jwtTokenCreator = new JwtTokenGenerator(appName);
            string token = jwtTokenCreator.GenerateJwt();

            // Get other necessary headers (Correlation ID, OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string oauthSig = "";
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);

                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {

                    // Assert the status code is 500 Internal Server Error using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");

                    // Read the response body and ensure it is empty.
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
    [TestRailCase(137685864)]
    [TestCategory("CreateAccount_Test_ValidatingExistingEmail")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_ValidatingExisitingEmail()
    {
        using var cts = new CancellationTokenSource();
        // Ensure that the Create Account test has been run first
        if (string.IsNullOrEmpty(DynamicUsername.Value))
        {
            false.ShouldBeTrue("The Create Account test must be run before this test.");
            return;
        }
        await CreateAccount_Test_ValidatingExistingEmail(DynamicUsername.Value, "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_ValidatingExistingEmail(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
    string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
    string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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


                    // Deserialize the response body into a dictionary
                    Dictionary<string, string>? responseJson = JsonSerializer.Deserialize<Dictionary<string, string>>(responseBody);

                    // Ensure the response is not null
                    _ = responseJson.ShouldNotBeNull("The response JSON should not be null.");

                    // Validate the code and message in the response
                    responseJson["code"].ShouldBe("111000", "Expected 'code' to be 111000.");
                    responseJson["message"].ShouldBe("Username is already taken", "Expected 'message' to be 'Username is already taken'.");

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
    [TestRailCase(137685865)]
    [TestCategory("CreateAccount_Test_MandatoryFieldValidation")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_MandatoryFieldValidation()
    {
        using var cts = new CancellationTokenSource();
        string username = $"testuser{DateTime.Now.Ticks}@gmail.com"; // Dynamic username
        await CreateAccount_Test_MandatoryFieldValidation(username, "SbxPa#$w0rd", "Test", "User", "US", "", "", "null", "", "US", "null",
        1, 1, "0", "en-US", "Automation", "abc", "abc", "fake", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_MandatoryFieldValidation(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
        string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
        string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(username))
        {
            throw new ArgumentException("Username cannot be empty.");
        }
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password cannot be empty.");
        }
        if (string.IsNullOrEmpty(firstName))
        {
            throw new ArgumentException("FirstName cannot be empty.");
        }
        if (string.IsNullOrEmpty(lastName))
        {
            throw new ArgumentException("LastName cannot be empty.");
        }
        if (string.IsNullOrEmpty(market))
        {
            throw new ArgumentException("Market cannot be empty.");
        }
        if (string.IsNullOrEmpty(registrationSource))
        {
            throw new ArgumentException("RegistrationSource cannot be empty.");
        }
           
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
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);

            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                AddHeaders(request, token, correlationId, xApiKey, oauthSig);

                // Setup content
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {

                    // Assert the status code is 201 created using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.Created, "Expected HTTP status code to be 201 Created.");

                    // Read the response body and ensure it is empty.
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
    [TestRailCase(137685998)]
    [TestCategory("CreateAccount_Test_With_Empty_Username")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_With_Empty_Username()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_With_Empty_Username("", "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_With_Empty_Username(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
        string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
        string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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

                    // Read the response body and ensure it is not empty
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrWhiteSpace("Expected the response body to contain some content.");

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

                    // Ensure the response contains the necessary keys
                    responseJson.ShouldContainKey("code", "Expected the response JSON to contain the key 'code'.");
                    responseJson.ShouldContainKey("message", "Expected the response JSON to contain the key 'message'.");

                    // Validate the 'code' and 'message' values in the response
                    responseJson["code"].ShouldBe("111008", "Expected 'code' to be 111008.");
                    responseJson["message"].ShouldBe("Please supply username", "Expected 'message' to be 'Please supply username'.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");

                    // Update Test Result in TestRail
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
    [TestRailCase(137685999)]
    [TestCategory("CreateAccount_Test_With_Empty_Password")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_With_Empty_Password()
    {
        using var cts = new CancellationTokenSource();
        string username = $"testuser{DateTime.Now.Ticks}@gmail.com";
        await CreateAccount_Test_With_Empty_Password(username, "", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_With_Empty_Password(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
        string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
        string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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

                    // Read the response body and ensure it is not empty
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrWhiteSpace("Expected the response body to contain some content.");

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

                    // Ensure the response contains the necessary keys
                    responseJson.ShouldContainKey("code", "Expected the response JSON to contain the key 'code'.");
                    responseJson.ShouldContainKey("message", "Expected the response JSON to contain the key 'message'.");

                    // Validate the 'code' and 'message' values in the response
                    responseJson["code"].ShouldBe("111011", "Expected 'code' to be 111011.");
                    responseJson["message"].ShouldBe("Please supply a password", "Expected 'message' to be 'Please supply a password'.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");

                    // Update Test Result in TestRail
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
    [TestRailCase(137686000)]
    [TestCategory("CreateAccount_Test_With_Empty_Firstname")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_With_Empty_Firstname()
    {
        using var cts = new CancellationTokenSource();
        string username = $"testuser{DateTime.Now.Ticks}@gmail.com";
        await CreateAccount_Test_With_Empty_Firstname(username, "SbxPa$$w0rd", "", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_With_Empty_Firstname(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
       string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
       string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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

                    // Read the response body and ensure it is not empty
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrWhiteSpace("Expected the response body to contain some content.");

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

                    // Ensure the response contains the necessary keys
                    responseJson.ShouldContainKey("code", "Expected the response JSON to contain the key 'code'.");
                    responseJson.ShouldContainKey("message", "Expected the response JSON to contain the key 'message'.");

                    // Validate the 'code' and 'message' values in the response
                    responseJson["code"].ShouldBe("111016", "Expected 'code' to be 111016.");
                    responseJson["message"].ShouldBe("Please supply a first name", "Expected 'message' to be 'Please supply a first name'.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");

                    // Update Test Result in TestRail
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
    [TestRailCase(137686001)]
    [TestCategory("CreateAccount_Test_With_Empty_Lastname")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_With_Empty_Lastname()
    {
        using var cts = new CancellationTokenSource();
        string username = $"testuser{DateTime.Now.Ticks}@gmail.com";
        await CreateAccount_Test_With_Empty_Lastname(username, "SbxPa$$w0rd", "Test", "", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_With_Empty_Lastname(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
       string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
       string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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

                    // Read the response body and ensure it is not empty
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrWhiteSpace("Expected the response body to contain some content.");

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

                    // Ensure the response contains the necessary keys
                    responseJson.ShouldContainKey("code", "Expected the response JSON to contain the key 'code'.");
                    responseJson.ShouldContainKey("message", "Expected the response JSON to contain the key 'message'.");

                    // Validate the 'code' and 'message' values in the response
                    responseJson["code"].ShouldBe("111015", "Expected 'code' to be 111015.");
                    responseJson["message"].ShouldBe("Please supply a last name", "Expected 'message' to be 'Please supply a last name'.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");

                    // Update Test Result in TestRail
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
    [TestRailCase(137686002)]
    [TestCategory("CreateAccount_Test_With_Empty_MarketCode")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_With_Empty_MarketCode()
    {
        using var cts = new CancellationTokenSource();
        string username = $"testuser{DateTime.Now.Ticks}@gmail.com";
        await CreateAccount_Test_With_Empty_MarketCode(username, "SbxPa$$w0rd", "Test", "User", "", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_With_Empty_MarketCode(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
       string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
       string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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

                    // Read the response body and ensure it is not empty
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrWhiteSpace("Expected the response body to contain some content.");

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

                    // Ensure the response contains the necessary keys
                    responseJson.ShouldContainKey("code", "Expected the response JSON to contain the key 'code'.");
                    responseJson.ShouldContainKey("message", "Expected the response JSON to contain the key 'message'.");

                    // Validate the 'code' and 'message' values in the response
                    responseJson["code"].ShouldBe("111012", "Expected 'code' to be 111012.");
                    responseJson["message"].ShouldBe("Please supply a market", "Expected 'message' to be 'Please supply a market'.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");

                    // Update Test Result in TestRail
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
    [TestRailCase(137686003)]
    [TestCategory("CreateAccount_Test_With_Empty_RegistrationSource")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_With_Empty_RegistrationSource()
    {
        using var cts = new CancellationTokenSource();
        string username = $"testuser{DateTime.Now.Ticks}@gmail.com";
        await CreateAccount_Test_With_Empty_RegistrationSource(username, "SbxPa$$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_With_Empty_RegistrationSource(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
       string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
       string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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

                    // Read the response body and ensure it is not empty
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrWhiteSpace("Expected the response body to contain some content.");

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

                    // Ensure the response contains the necessary keys
                    responseJson.ShouldContainKey("code", "Expected the response JSON to contain the key 'code'.");
                    responseJson.ShouldContainKey("message", "Expected the response JSON to contain the key 'message'.");

                    // Validate the 'code' and 'message' values in the response
                    responseJson["code"].ShouldBe("111009", "Expected 'code' to be 111009.");
                    responseJson["message"].ShouldBe("Please supply a registration source", "Expected 'message' to be 'Please supply a registration source'.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");

                    // Update Test Result in TestRail
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
    [TestRailCase(137686004)]
    [TestCategory("CreateAccount_Test_InvalidUsername")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_InvalidUsername()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_InvalidUsername("abc", "SbxPa$$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_InvalidUsername(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
       string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
       string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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

                    // Read the response body and ensure it is not empty
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrWhiteSpace("Expected the response body to contain some content.");

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

                    // Ensure the response JSON is not null
                    _ = responseJson.ShouldNotBeNull("The response JSON should not be null.");
                    responseJson.ShouldNotBeEmpty("The response JSON should contain at least one key-value pair.");

                    // Ensure the response contains the necessary keys
                    responseJson.ShouldContainKey("code", "Expected the response JSON to contain the key 'code'.");
                    responseJson.ShouldContainKey("message", "Expected the response JSON to contain the key 'message'.");

                    // Validate the 'code' and 'message' values in the response
                    responseJson["code"].ShouldBe("111041", "Expected 'code' to be 111041.");
                    responseJson["message"].ShouldBe("Invalid email address.", "Expected 'message' to be 'Invalid email address.'.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");

                    // Update Test Result in TestRail
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
    [TestRailCase(137686005)]
    [TestCategory("CreateAccount_Test_InvalidPassword")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_InvalidPassword()
    {
        using var cts = new CancellationTokenSource();
        string username = $"testuser{DateTime.Now.Ticks}@sbuxautomation.com";
        await CreateAccount_Test_InvalidPassword(username, "-", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_InvalidPassword(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
       string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
       string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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

                    // Read the response body and ensure it is not empty
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrWhiteSpace("Expected the response body to contain some content.");

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

                    // Ensure the response JSON is not null
                    _ = responseJson.ShouldNotBeNull("The response JSON should not be null.");
                    responseJson.ShouldNotBeEmpty("The response JSON should contain at least one key-value pair.");

                    // Ensure the response contains the necessary keys
                    responseJson.ShouldContainKey("code", "Expected the response JSON to contain the key 'code'.");
                    responseJson.ShouldContainKey("message", "Expected the response JSON to contain the key 'message'.");

                    // Validate the 'code' and 'message' values in the response
                    responseJson["code"].ShouldBe("111022", "Expected 'code' to be 111022.");
                    responseJson["message"].ShouldBe("Password does not meet complexity requirements", "Expected 'message' to be 'Password does not meet complexity requirements.'");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed - Validated code and message successfully.");

                    // Update Test Result in TestRail
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
    [TestRailCase(137686006)]
    [TestCategory("CreateAccount_Test_InvalidFirstname")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_InvalidFirstname()
    {
        using var cts = new CancellationTokenSource();
        string username = $"testuser{DateTime.Now.Ticks}@sbuxautomation.com";
        await CreateAccount_Test_InvalidFirstname(username, "SbxPa$$w0rd", "{{", "test", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_InvalidFirstname(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
       string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
       string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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

                    // Read the response body and ensure it is not empty
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrWhiteSpace("Expected the response body to contain some content.");

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

                    // Ensure the response JSON is not null
                    _ = responseJson.ShouldNotBeNull("The response JSON should not be null.");
                    responseJson.ShouldNotBeEmpty("The response JSON should contain at least one key-value pair.");

                    // Ensure the response contains the necessary keys
                    responseJson.ShouldContainKey("code", "Expected the response JSON to contain the key 'code'.");
                    responseJson.ShouldContainKey("message", "Expected the response JSON to contain the key 'message'.");

                    // Validate the 'code' and 'message' values in the response
                    responseJson["code"].ShouldBe("111036", "Expected 'code' to be 111036.");
                    responseJson["message"].ShouldBe("Invalid characters specified for first and/or last name.",
                        "Expected 'message' to be 'Invalid characters specified for first and/or last name.'");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed - Validated code and message successfully.");

                    // Update Test Result in TestRail
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
    [TestRailCase(137686007)]
    [TestCategory("CreateAccount_Test_InvalidLastname")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_InvalidLastname()
    {
        using var cts = new CancellationTokenSource();
        string username = $"testuser{DateTime.Now.Ticks}@sbuxautomation.com";
        await CreateAccount_Test_InvalidLastname(username, "SbxPa$$w0rd", "Test", "}}", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_InvalidLastname(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
       string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
       string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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

                    // Read the response body and ensure it is not empty
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrWhiteSpace("Expected the response body to contain some content.");

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

                    // Ensure the response JSON is not null
                    _ = responseJson.ShouldNotBeNull("The response JSON should not be null.");
                    responseJson.ShouldNotBeEmpty("The response JSON should contain at least one key-value pair.");

                    // Ensure the response contains the necessary keys
                    responseJson.ShouldContainKey("code", "Expected the response JSON to contain the key 'code'.");
                    responseJson.ShouldContainKey("message", "Expected the response JSON to contain the key 'message'.");

                    // Validate the 'code' and 'message' values in the response
                    responseJson["code"].ShouldBe("111036", "Expected 'code' to be 111036.");
                    responseJson["message"].ShouldBe("Invalid characters specified for first and/or last name.",
                        "Expected 'message' to be 'Invalid characters specified for first and/or last name.'");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed - Validated code and message successfully.");

                    // Update Test Result in TestRail
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
    [TestRailCase(137686008)]
    [TestCategory("CreateAccount_Test_InvalidMarketCode")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_InvalidMarketCode()
    {
        using var cts = new CancellationTokenSource();
        string username = $"testuser{DateTime.Now.Ticks}@sbuxautomation.com";
        await CreateAccount_Test_InvalidMarketCode(username, "SbxPa$$w0rd", "Test", "User", "IN", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_InvalidMarketCode(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
       string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
       string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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

                    // Read the response body and ensure it is not empty
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrWhiteSpace("Expected the response body to contain some content.");

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

                    // Ensure the response JSON is not null
                    _ = responseJson.ShouldNotBeNull("The response JSON should not be null.");
                    responseJson.ShouldNotBeEmpty("The response JSON should contain at least one key-value pair.");

                    // Ensure the response contains the necessary keys
                    responseJson.ShouldContainKey("code", "Expected the response JSON to contain the key 'code'.");
                    responseJson.ShouldContainKey("message", "Expected the response JSON to contain the key 'message'.");

                    // Validate the 'code' and 'message' values in the response
                    responseJson["code"].ShouldBe("111367", "Expected 'code' to be 111367.");
                    responseJson["message"].ShouldBe("Invalid Market.", "Expected 'message' to be 'Invalid Market.'");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed - Validated code and message successfully.");

                    // Update Test Result in TestRail
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
    [TestRailCase(137686009)]
    [TestCategory("CreateAccount_Test_EmptyRequestBody")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_EmptyRequestBody()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_EmptyRequestBody(GlobalVariables.AppNameValue!, cts.Token);
    }
    private async Task CreateAccount_Test_EmptyRequestBody(string appName, CancellationToken cancellationToken)
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
            string requestBody = "";


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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
                    responseBody.ShouldNotBeEmpty("Response body should not be empty.");
                    responseBody.ShouldContain("A non-empty request body is required.");

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
    [TestRailCase(137686010)]
    [TestCategory("CreateAccount_Test_EmptyRequestValues")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_EmptyRequestValues()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_EmptyRequestValues("", "", "", "", "", "", "", "", "", "", "", 0, 0, "", "", "", "", "", "", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_EmptyRequestValues(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
       string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
       string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

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

                    // Read the response body and ensure it is not empty
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    responseBody.ShouldNotBeNullOrWhiteSpace("Expected the response body to contain some content.");

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

                    // Ensure the response contains the necessary keys
                    responseJson.ShouldContainKey("code", "Expected the response JSON to contain the key 'code'.");
                    responseJson.ShouldContainKey("message", "Expected the response JSON to contain the key 'message'.");

                    // Validate the 'code' and 'message' values in the response
                    responseJson["code"].ShouldBe("111012", "Expected 'code' to be 111012.");
                    responseJson["message"].ShouldBe("Please supply a market", "Expected 'message' to be 'Please supply a market'.");

                    ExtentReportManager.Log($"Response Status: {response.StatusCode}");
                    ExtentReportManager.LogPass("Test Passed");

                    // Update Test Result in TestRail
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
            ExtentReportManager.LogFail("Test failed.");
            throw;
        }
        finally
        {
            ExtentReportManager.Log("Test Ended.");
        }
    }

    [TestMethod]
    [TestRailCase(137687519)]
    [TestCategory("CreateAccount_Test_Missing_CorrelationId")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_Missing_CorrelationId()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_Missing_CorrelationId("testuset7s45583@sbuxautomation.com", "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_Missing_CorrelationId(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
    string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
    string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Generate a JWT token for API authentication.
            var jwtTokenCreator = new JwtTokenGenerator(appName);
            string token = jwtTokenCreator.GenerateJwt();

            // Get other necessary headers (Correlation ID, OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                // Missing Correlation Id
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-key", xApiKey);
                request.Headers.Add("x-api-sig", oauthSig);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {

                    // Assert the status code is 500 Internal Server Error using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");

                    // Read the response body and ensure it is empty.
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
    [TestRailCase(137687520)]
    [TestCategory("CreateAccount_Test_MissingXApiKey")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_MissingXApiKey()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_MissingXApiKey("testuset7s45583@sbuxautomation.com", "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_MissingXApiKey(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
        string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
        string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
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
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                // Missing XApiKey
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-sig", oauthSig);
                request.Headers.Add("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {

                    // Assert the status code is 500 Internal Server Error using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 Internal Server Error.");

                    // Read the response body and ensure it is not empty.
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
    [TestRailCase(137687521)]
    [TestCategory("CreateAccount_Test_MissingXApiSig")]
    [TestCategory("P1")]
    [TestCategory("DPCE-4195")]
    public async Task Test_CreateAccount_MissingXApiSig()
    {
        using var cts = new CancellationTokenSource();
        await CreateAccount_Test_MissingXApiSig("testuset7s45583@sbuxautomation.com", "SbxPa#$w0rd", "Test", "User", "US", "123 Any Street", "Apt. B", "Seattle", "WA", "US", "5555551212",
        5, 4, "98001", "en-US", "Automation", "iOS", "10.75.23.112", "fakeFingerprint", GlobalVariables.AppNameValue!, cts.Token);
    }

    private async Task CreateAccount_Test_MissingXApiSig(string username, string password, string firstName, string lastName, string market, string addressLine1, string addressLine2, string city,
        string countrySubdivision, string country, string mobilePhoneNumber, int birthMonth, int birthDay, string postalCode, string preferredCulture, string registrationSource, string riskPlatform,
        string riskIpAddress, string riskDeviceFingerprint, string appName, CancellationToken cancellationToken)
    {
        try
        {
            // Generate a JWT token for API authentication.
            var jwtTokenCreator = new JwtTokenGenerator(appName);
            string token = jwtTokenCreator.GenerateJwt();

            // Get other necessary headers (Correlation ID, OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(appName);
            string correlationId = acObj.CorrelationId;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = CreateAccountPayload(
            username, password, firstName, lastName, market, addressLine1, addressLine2, city, countrySubdivision, country, mobilePhoneNumber, birthMonth, birthDay, postalCode,
            preferredCulture, registrationSource, riskPlatform, riskIpAddress, riskDeviceFingerprint);


            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.CreateAccountApi}";

            // Send the API request.
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                // Missing XApiSig
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("x-api-key", xApiKey);
                request.Headers.Add("X-Correlation-Id", correlationId);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken))
                {

                    // Assert the status code is 500 InternalServerError using Shouldly.
                    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "Expected HTTP status code to be 500 InternalServerError.");

                    // Read the response body and ensure it is empty.
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
    private void AddHeaders(HttpRequestMessage request, string jwtToken, string correlationId, string? xApiKey, string? oauthSig)
    {
        // Authorization header With Bearer token.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
        // Custom headers required for the API.
        request.Headers.Add("x-api-key", xApiKey);
        request.Headers.Add("x-api-sig", oauthSig);
        //request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Correlation-Id", correlationId);
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
        // Build the payload With all required fields
        var requestData = new ACQModel.CreateAccountModel
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
            Risk = new ACQModel.RiskModel
            {
                Platform = riskPlatform,
                Reputation = new ACQModel.ReputationModel
                {
                    IpAddress = riskIpAddress,
                    DeviceFingerprint = riskDeviceFingerprint
                }
            }
        };

        // Serialize the payload to a JSON string
        return JsonSerializer.Serialize(requestData, new JsonSerializerOptions
        {
            WriteIndented = true // Optional for debugging
        });
    }
}
