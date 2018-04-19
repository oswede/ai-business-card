using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IBM.Watson.DeveloperCloud.Services.TextToSpeech.v1;
using IBM.Watson.DeveloperCloud.Utilities;
using IBM.Watson.DeveloperCloud.Logging;
using IBM.Watson.DeveloperCloud.Connection;

public class tts_handler : MonoBehaviour {

    private TextToSpeech _textToSpeech;

    private string tts_username = "cc372924-80a7-42b1-9461-733a98ac7c6a";
    private string tts_password = "73rsJlGpJiRP";
    private string tts_url = "https://stream.watsonplatform.net/text-to-speech/api";

    private bool isPlaying;
    private TTSResponse callback;

    void Start () {
        LogSystem.InstallDefaultReactors();

        Credentials credentials = new Credentials(tts_username, tts_password, tts_url);
        _textToSpeech = new TextToSpeech(credentials);

        _textToSpeech.Voice = VoiceType.en_US_Michael;

        isPlaying = false;
    }

    public void Synthesize(string input)
    {
        if (!_textToSpeech.ToSpeech(OnSynthesize, OnFail, input))
            Log.Debug("TTS.ToSpeech()", "Failed to synthesize!");
    }

    private void OnSynthesize(AudioClip clip, Dictionary<string, object> customData)
    {
        callback.ttsResponseReceived(clip);
    }

    private void OnFail(RESTConnector.Error error, Dictionary<string, object> customData)
    {
        Log.Error("TTS.OnFail()", "Error received: {0}", error.ToString());
    }

    public void setCallback(ServiceManager newCallback)
    {
        callback = newCallback;
    }

    public interface TTSResponse
    {
        void ttsResponseReceived(AudioClip lastResponse); // show output and move on to next stage
    }

}