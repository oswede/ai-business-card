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
    private string convo_username = "fc30b2dc-b2e7-4358-b5e9-1c40aec50a7e";
    private string convo_password = "UZiUgW3HcVCG";
    private string convo_url = "https://gateway.watsonplatform.net/conversation/api";
    private string convo_workspaceId = "82021a7f-243d-4060-88d1-612d5bf7d963";
    private string _conversationVersionDate = "2018-02-21";

    private fsSerializer _serializer = new fsSerializer();
    private Dictionary<string, object> _context = null;

    private string convo_output; // output of the conversation to
    public Text convo_output_display; // display conversation output

    void Start () {

        LogSystem.InstallDefaultReactors();

        Credentials convo_credentials = new Credentials(convo_username, convo_password, convo_url);
        _conversation = new Conversation(convo_credentials);
        _conversation.VersionDate = _conversationVersionDate;

        Message(null); // send initial null message to the conversation to get the first response
    }

    public void Message(string nextMessage)
    {
        if (!_conversation.Message(OnMessageSuccess, OnMessageFail, convo_workspaceId, nextMessage))
        {
            Log.Debug("CONVO.Message()", "Failed to message!");
        }
    }

    private void OnMessageSuccess(object resp, Dictionary<string, object> customData)
    {
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

    }

    private void OnMessageFail(RESTConnector.Error error, Dictionary<string, object> customData)
    {
        Log.Error("CONVO.HandleFail()", "Error received: {0}", error.ToString());
    }

}