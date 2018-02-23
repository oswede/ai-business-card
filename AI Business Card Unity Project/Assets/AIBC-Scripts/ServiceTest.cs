using UnityEngine;
using System.Collections;
using IBM.Watson.DeveloperCloud.Logging;
using IBM.Watson.DeveloperCloud.Services.SpeechToText.v1;
using IBM.Watson.DeveloperCloud.Services.Conversation.v1;
using IBM.Watson.DeveloperCloud.Services.TextToSpeech.v1;
using IBM.Watson.DeveloperCloud.Utilities;
using IBM.Watson.DeveloperCloud.DataTypes;
using System.Collections.Generic;
using UnityEngine.UI;

using FullSerializer;
using IBM.Watson.DeveloperCloud.Connection;

// this is an example of hwo to take the input, send it to the conversation, send that response to speech to text, and output the result
// also display the speech to text result at the top, and also the conversation output result at the bottom

public class ServiceTest : MonoBehaviour
{

    /*Speech to text stuff */
    public AudioClip stt_input; // input to the speech to text
    private SpeechToText _speechToText;
    private string stt_username = "8f137eeb-cb17-47d4-afea-36b21f6982f0";
    private string stt_password = "tMawRjCtV56K";
    private string stt_url = "https://stream.watsonplatform.net/speech-to-text/api";

    private int _recordingRoutine = 0;
    private string _microphoneID = null;
    private AudioClip _recording = null;
    private int _recordingBufferSize = 1;
    private int _recordingHZ = 22050;

    private string stt_output; // output of the speech to text to send to conversation
    public Text stt_to_convo; // display output of the speech to text

    /* Conversation stuff */
    private Conversation _conversation;
    private string convo_username = "fc30b2dc-b2e7-4358-b5e9-1c40aec50a7e";
    private string convo_password = "UZiUgW3HcVCG";
    private string convo_url = "https://gateway.watsonplatform.net/conversation/api";
    private string convo_workspaceId = "82021a7f-243d-4060-88d1-612d5bf7d963";
    private string _conversationVersionDate = "2018-02-21";

    private fsSerializer _serializer = new fsSerializer();
    private Dictionary<string, object> _context = null;

    private string convo_output; // output of the conversation to
    public Text convo_to_tts; // display conversation output

    private TextToSpeech _textToSpeech;
    private string tts_username = "";
    private string tts_password = "";
    private string tts_url = "";

    private List<AudioClip> responses; //maybe to be used to store the previous speech to text conversions if it needs to revert back to an older one

    public AudioClip tts_output; // output from the text to speech

    void Start()
    {
        LogSystem.InstallDefaultReactors();

        //  Create credential and instantiate service
        Credentials stt_credentials = new Credentials(stt_username, stt_password, stt_url);
        Credentials convo_credentials = new Credentials(convo_username, convo_password, convo_url);

        _speechToText = new SpeechToText(stt_credentials);
        _conversation = new Conversation(convo_credentials);
        _conversation.VersionDate = _conversationVersionDate;

        stt_to_convo = GameObject.Find("stt_to_convo").GetComponent<Text>();
        convo_to_tts = GameObject.Find("convo_to_tts").GetComponent<Text>();

        STT_Active = true; // keep the connection active throughout to reduce overhead

        Message(null); // get the initial welcome response
    }

    public void Go()
    {
        StartRecording();
    }


    /* Methods for the Speech to Text" */
    public bool STT_Active
    {
        get { return _speechToText.IsListening; }
        set
        {
            if (value && !_speechToText.IsListening) // if not listening but is set to true, start listening
            {
                _speechToText.DetectSilence = true;
                _speechToText.EnableWordConfidence = true;
                _speechToText.EnableTimestamps = true;
                _speechToText.SilenceThreshold = 0.01f;
                _speechToText.MaxAlternatives = 0;
                _speechToText.EnableInterimResults = true;
                _speechToText.OnError = OnError;
                _speechToText.InactivityTimeout = -1;
                _speechToText.ProfanityFilter = false;
                _speechToText.SmartFormatting = true;
                _speechToText.SpeakerLabels = false;
                _speechToText.WordAlternativesThreshold = null;
                _speechToText.StartListening(OnRecognize, OnRecognizeSpeaker);
            }
            else if (!value && _speechToText.IsListening) // if listening and set to false, stop listening
            {
                _speechToText.StopListening();
            }
        }
    }

    private void StartRecording()
    {
        if (_recordingRoutine == 0)
        {
            UnityObjectUtil.StartDestroyQueue();
            _recordingRoutine = Runnable.Run(RecordingHandler());
        }
    }

    public void StopRecording()
    {
        if (_recordingRoutine != 0)
        {
            Microphone.End(_microphoneID);
            Runnable.Stop(_recordingRoutine);
            _recordingRoutine = 0;
        }
    }

    private void OnError(string error)
    {
        STT_Active = false;
        Log.Debug("STT.OnError()", "Error! {0}", error);
    }

    private IEnumerator RecordingHandler()
    {
        Log.Debug("STT.RecordingHandler()", "devices: {0}", Microphone.devices);
        _recording = Microphone.Start(_microphoneID, true, _recordingBufferSize, _recordingHZ);
        yield return null;      // let _recordingRoutine get set..

        if (_recording == null)
        {
            StopRecording();
            yield break;
        }

        bool bFirstBlock = true;
        int midPoint = _recording.samples / 2;
        float[] samples = null;

        while (_recordingRoutine != 0 && _recording != null)
        {
            int writePos = Microphone.GetPosition(_microphoneID);
            if (writePos > _recording.samples || !Microphone.IsRecording(_microphoneID))
            {
                Log.Error("STT.RecordingHandler()", "Microphone disconnected.");

                StopRecording();
                yield break;
            }

            if ((bFirstBlock && writePos >= midPoint)
              || (!bFirstBlock && writePos < midPoint))
            {
                // front block is recorded, make a RecordClip and pass it onto our callback.
                samples = new float[midPoint];
                _recording.GetData(samples, bFirstBlock ? 0 : midPoint);

                AudioData record = new AudioData();
                record.MaxLevel = Mathf.Max(Mathf.Abs(Mathf.Min(samples)), Mathf.Max(samples));
                record.Clip = AudioClip.Create("Recording", midPoint, _recording.channels, _recordingHZ, false);
                record.Clip.SetData(samples, 0);

                _speechToText.OnListen(record);

                bFirstBlock = !bFirstBlock;
            }
            else
            {
                // calculate the number of samples remaining until we ready for a block of audio, 
                // and wait that amount of time it will take to record.
                int remaining = bFirstBlock ? (midPoint - writePos) : (_recording.samples - writePos);
                float timeRemaining = (float)remaining / (float)_recordingHZ;

                yield return new WaitForSeconds(timeRemaining);
            }

        }

        yield break;
    }

    private void OnRecognize(SpeechRecognitionEvent result)
    {
        if (result != null && result.results.Length > 0)
        {
            foreach (var res in result.results)
            {
                foreach (var alt in res.alternatives)
                {
                    //string text = string.Format("{0} ({1}, {2:0.00})\n", alt.transcript, res.final ? "Final" : "Interim", alt.confidence);
                    stt_output = string.Format("{0}", alt.transcript);
                    Log.Debug("STT.OnRecognize()", stt_output);
                    stt_to_convo.text = stt_output;                   

                    // if it's the final result, set received to true
                    if (res.final)
                    {
                        Message(stt_output);
                    }

                }

                if (res.keywords_result != null && res.keywords_result.keyword != null)
                {
                    foreach (var keyword in res.keywords_result.keyword)
                    {
                        Log.Debug("STT.OnRecognize()", "keyword: {0}, confidence: {1}, start time: {2}, end time: {3}", keyword.normalized_text, keyword.confidence, keyword.start_time, keyword.end_time);
                    }
                }

                if (res.word_alternatives != null)
                {
                    foreach (var wordAlternative in res.word_alternatives)
                    {
                        Log.Debug("STT.OnRecognize()", "Word alternatives found. Start time: {0} | EndTime: {1}", wordAlternative.start_time, wordAlternative.end_time);
                        foreach (var alternative in wordAlternative.alternatives)
                            Log.Debug("STT.OnRecognize()", "\t word: {0} | confidence: {1}", alternative.word, alternative.confidence);
                    }
                }
            }
        }
    }

    private void OnRecognizeSpeaker(SpeakerRecognitionEvent result)
    {
        if (result != null)
        {
            foreach (SpeakerLabelsResult labelResult in result.speaker_labels)
            {
                Log.Debug("STT.OnRecognize()", string.Format("speaker result: {0} | confidence: {3} | from: {1} | to: {2}", labelResult.speaker, labelResult.from, labelResult.to, labelResult.confidence));
            }
        }
    }

    /* Methods for the Conversation */
    private void Message(string nextMessage)
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

        if (resp != null && (messageResponse.output.text.Length > 0))
        {
            string output = messageResponse.output.text[0];

            Debug.Log("Output: " + output);
        }

    }

    private void OnMessageFail(RESTConnector.Error error, Dictionary<string, object> customData)
    {
        Log.Error("CONVO.HandleFail()", "Error received: {0}", error.ToString());
    }

}