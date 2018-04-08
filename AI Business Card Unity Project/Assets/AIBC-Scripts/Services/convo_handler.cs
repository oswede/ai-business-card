using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IBM.Watson.DeveloperCloud.Services.Conversation.v1;
using IBM.Watson.DeveloperCloud.Logging;
using IBM.Watson.DeveloperCloud.Utilities;
using UnityEngine.UI;
using IBM.Watson.DeveloperCloud.Connection;
using FullSerializer;

public class convo_handler : MonoBehaviour {

    private Conversation _conversation;
    private string convo_username = "9b4aecd0-47ca-4b13-a055-342e21219a3b";
    private string convo_password = "p5nRkHgDlm5S";
    private string convo_url = "https://gateway.watsonplatform.net/conversation/api";
    private string convo_workspaceId = "591b3c20-f135-4e3f-85be-91d3452d6b31";
    private string _conversationVersionDate = "2018-03-04";

    private fsSerializer _serializer = new fsSerializer();
    private Dictionary<string, object> _context = null;

    private string convo_output; // output of the conversation to
    public Text convo_output_display; // display conversation output

    private bool convoResponseReceived;

    void Start () {

        LogSystem.InstallDefaultReactors();

        Credentials convo_credentials = new Credentials(convo_username, convo_password, convo_url);
        _conversation = new Conversation(convo_credentials);
        _conversation.VersionDate = _conversationVersionDate;

        convoResponseReceived = false;

        Message(null); // send initial null message to the conversation to get the first response
    }

    public void Message(string nextMessage)
    {
        MessageRequest messageRequest = new MessageRequest()
        {
            input = new Dictionary<string, object>()
            {
                { "text", nextMessage }
            },
            context = _context
        };

        if (!_conversation.Message(OnMessageSuccess, OnMessageFail, convo_workspaceId, messageRequest))
        {
            Log.Debug("CONVO.Message()", "Failed to message!");
        }
    }


    private void OnMessageSuccess(object resp, Dictionary<string, object> customData)
    {

        object _tempContext = null;
        (resp as Dictionary<string, object>).TryGetValue("context", out _tempContext);

        if (_tempContext != null)
            _context = _tempContext as Dictionary<string, object>;
        else
            Log.Debug("ExampleConversation.OnMessageSuccess()", "Failed to get context");


        Log.Debug("CONVO.OnMessage()", "Conversation: Message Response: {0}", customData["json"].ToString());

        //  Convert resp to fsdata
        fsData fsdata = null;
        fsResult r = _serializer.TrySerialize(resp.GetType(), resp, out fsdata);
        if (!r.Succeeded)
            throw new WatsonException(r.FormattedMessages);

        //  Convert fsdata to MessageResponse
        MessageResponse messageResponse = new MessageResponse();
        object obj = messageResponse;
        r = _serializer.TryDeserialize(fsdata, obj.GetType(), ref obj);
        if (!r.Succeeded)
            throw new WatsonException(r.FormattedMessages);

        // Extract response from output
        if (resp != null && (messageResponse.output.text.Length > 0))
        {
            convo_output = "";

            for (int i=0; i < messageResponse.output.text.Length; i++)
            {
                convo_output += messageResponse.output.text[i] + "\n";
            }

            convo_output_display.text = convo_output;
        }

        convoResponseReceived = true;

    }

    private void OnMessageFail(RESTConnector.Error error, Dictionary<string, object> customData)
    {
        Log.Error("CONVO.HandleFail()", "Error received: {0}", error.ToString());
    }

    public string getLastConvoOutput()
    {
        return convo_output;
    }

    public bool hasNextConvoResponse()
    {
        return convoResponseReceived;
    }

    public void waitForNextConvoResponse()
    {
        convoResponseReceived = false;
    }

    public void Toggle_Changed(bool newValue)
    {
        convo_output_display.enabled = newValue;
    }

}