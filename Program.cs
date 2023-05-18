using Azure.Communication.CallAutomation;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Azure.Communication;
using Azure.Messaging;
using Azure.Messaging.EventGrid;


//simple sdk demo 
//step 1 - create an azure portal account
//step 2 - create an azure communication services resource
//step 3 - find the connection string for the resource
//step 4 - download ngrok and start a session for our port
//step 5 - start the service and register the event for download file status
//step 6 - start 2 acs test apps - https://acs-sample-app.azurewebsites.net/
//step 7 - start the dotnet project
//step 8 - start the call
//step 9 - answer call
//step 10 - end the call
//step 11 - download file, delete file

/*disclaimer
this was originally made for quick and easy testing of api endpoints. Best practices and structure of project is not here. 
for best practices in creating something similar please see the following
https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/call-automation/callflows-for-customer-interactions?pivots=programming-language-csharp#build-a-customer-interaction-workflow-using-call-automation
https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/voice-video-calling/get-started-call-recording?pivots=programming-language-csharp
*/

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

//setup required variables for our service
const string ngrokEndpoint = ""; //ngrok endpoint for our service
string recID = ""; // store recording id to easily test other recording functions with curl commands
string deleteLocation = ""; // store recording delete location
string contentLocation = ""; // store recording download location
const string cstring = ""; // Input your connection string here 

var client = new CallAutomationClient(connectionString: cstring);

// Start a call manually and start recording
app.MapGet("/startcall", (
    [FromQuery] string acsTarget) =>
    {
        Debug.WriteLine("start call endpoint");
        Debug.WriteLine("starting a new call to user:" + acsTarget);
        CommunicationUserIdentifier targetUser = new CommunicationUserIdentifier(acsTarget);
        var targets = new List<CommunicationUserIdentifier>
        {
           targetUser
        };
        CommunicationUserIdentifier sourceUser = new CommunicationUserIdentifier("");
        var source = new CallSource(sourceUser);
        var createCallOptions = new CreateCallOptions(source, targets, new Uri(ngrokEndpoint+ "/startrecording"));
        client.CreateCall(createCallOptions);
        return Results.Ok();
    }
);


app.MapPost("/startrecording", ([FromBody] CloudEvent[] cloudEvents) =>
{
    Debug.WriteLine("start recording endpoint");

    foreach (var cloudEvent in cloudEvents)
    {
            CallAutomationEventBase @event = CallAutomationEventParser.Parse(cloudEvent);
            if (@event is CallConnected)
            {
                Debug.WriteLine("CALL Connected event found");
                StartRecordingOptions recordingOptions = new StartRecordingOptions(new ServerCallLocator(@event.ServerCallId))
                {
                    RecordingContent = RecordingContent.Audio,
                    RecordingChannel = RecordingChannel.Mixed,
                    RecordingFormat = RecordingFormat.Wav,
                    RecordingStateCallbackEndpoint = new Uri(ngrokEndpoint + "/callback")
                };

                RecordingStateResult startRecordingResult = client.GetCallRecording().StartRecording(recordingOptions);
                recID = startRecordingResult.RecordingId;
            }
    }
    return Results.Ok();
});


app.MapPost("/callback", () =>
{
    return Results.Ok();
});

app.MapPost("/download", ([FromBody] EventGridEvent[] eventGridEvents) =>
{
    Debug.WriteLine("download endpoint");
    foreach (var eventGridEvent in eventGridEvents)
    {
        if (eventGridEvent.TryGetSystemEventData(out object eventData))
        {
            // Handle the webhook subscription validation event.
            if (eventData is Azure.Messaging.EventGrid.SystemEvents.SubscriptionValidationEventData subscriptionValidationEventData)
            {
                var responseData = new Azure.Messaging.EventGrid.SystemEvents.SubscriptionValidationResponse
                {
                    ValidationResponse = subscriptionValidationEventData.ValidationCode
                };
                return Results.Ok(responseData);
            }

            if (eventData is Azure.Messaging.EventGrid.SystemEvents.AcsRecordingFileStatusUpdatedEventData statusUpdated)
            {
                contentLocation = statusUpdated.RecordingStorageInfo.RecordingChunks[0].ContentLocation;
                deleteLocation = statusUpdated.RecordingStorageInfo.RecordingChunks[0].DeleteLocation;
                Debug.WriteLine(contentLocation);
                Debug.WriteLine(deleteLocation);
            }
        }
    }
    return Results.Ok();
});

app.MapGet("/pause", () =>
{
    Debug.WriteLine("pause endpoint");

    client.GetCallRecording().PauseRecording(recID);

    return Results.Ok();
});

app.MapGet("/stop", () =>
{
    Debug.WriteLine("stop endpoint");

    client.GetCallRecording().StopRecording(recID);

    return Results.Ok();
});

app.MapGet("/resume", () =>
{
    Debug.WriteLine("resume endpoint");

    client.GetCallRecording().ResumeRecording(recID);

    return Results.Ok();
});

app.MapGet("/deleterec", () =>
{
    Debug.WriteLine("delete endpoint");

    client.GetCallRecording().DeleteRecording(new Uri(deleteLocation));

    return Results.Ok();
});

app.MapGet("/downloadrec", () =>
{
    Debug.WriteLine("download endpoint");

    client.GetCallRecording().DownloadTo(new Uri(contentLocation), "testfile.wav");

    return Results.Ok();
});

app.Run();