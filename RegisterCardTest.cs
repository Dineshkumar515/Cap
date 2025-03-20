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

[TestClass]
public class RegisterCardTest
{
    private static readonly HttpClient HttpClient = new();
    public TestContext? TestContext { get; set; }
    private static TestContext? classTestContext;
    private static readonly int TestSuiteRunId = Global.TestSuiteRunId;
    private static readonly ConcurrentDictionary<int, (string, int)> TestResultStore
       = new();
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
    [TestRailCase(137686011)]
    [TestCategory("RegisterCard_Test_HappyPath")]
    [TestCategory("P0")]
    [TestCategory("DPCE-4200")]
    public async Task Test_RegisterCard_HappyPath()
    {
        using var cts = new CancellationTokenSource();
        string cardNumber = "7777475507809080"; // Dynamic card number
        DynamicCardNumber.Value = cardNumber;
        await RegisterCard_Test_HappyPath(cardNumber, "71361803", "test", "US", true, true, "web", "Teavana", "IOS", "US", true, "7Qg-á3Kw-á4Sg-á2Mw-á", cts.Token);
    }

    private async Task RegisterCard_Test_HappyPath(string cardNumber, string pin, string nickname, string submarket, bool primary, bool register, string platform, string marketing, string riskPlatform, string riskMarket,
        bool isLoggedIn, string ccAgentName, CancellationToken cancellationToken)
    {
        try
        {
            // Generate a JWT token for API authentication.
            var jwtTokenCreator = new JwtTokenGenerator(GlobalVariables.AppNameValue!);
            string token = jwtTokenCreator.GenerateJwt();

            // Get other necessary headers (Correlation ID, OAuth signature).
            var acObj = new OauthTokenAndCorrelationId(GlobalVariables.AppNameValue!);
            string correlationId = acObj.CorrelationId;
            string oauthSig = acObj.OauthSig;
            string xApiKey = GlobalVariables.ClientId ?? throw new ArgumentNullException(nameof(GlobalVariables.ClientId), "Client ID cannot be null.");

            // Convert the updated JSON object to a string
            string requestBody = RegisterCardPayload(cardNumber, pin, nickname, submarket, primary, register, platform, marketing, riskPlatform, riskMarket, isLoggedIn, ccAgentName);

            // Construct the API URL With query parameters.
            string url = $"{GlobalVariables.BaseUrl}{ApiEndpoints.RegisterCardApi}";

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
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Correlation-Id", correlationId);
    }

    // Register Card Payload Preparation Using Model
    public static string RegisterCardPayload(
     string cardNumber,
     string pin,
     string nickname,
     string submarket,
     bool primary,
     bool register,
     string platform,
     string marketing,
     string riskPlatform,
     string riskMarket,
     bool isLoggedIn,
     string ccAgentName)
    {
        // Build the payload With all required fields
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

        // Serialize the payload to a JSON string
        return JsonSerializer.Serialize(requestData, new JsonSerializerOptions
        {
            WriteIndented = true // Optional for debugging
        });
    }
}
