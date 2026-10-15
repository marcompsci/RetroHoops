// Retro Hoops — the play-by-play voice (original code, 15 Oct 2026).
// Speaks THE STREETS commentary with the iPhone's built-in speech voices (AVSpeechSynthesizer): offline, nothing
// downloaded. Called from C# via [DllImport("__Internal")] (Core/Speech.cs). It uses the app's audio session as it
// is (so it plays alongside the game's music and sound), and a new line cuts off the one before.
#import <AVFoundation/AVFoundation.h>

static AVSpeechSynthesizer *gSynth = nil;

extern "C" {

void RetroSpeech_Say(const char *text, const char *language, float rate, float pitch, float volume)
{
    if (text == NULL) return;
    NSString *line = [NSString stringWithUTF8String:text];
    NSString *lang = language != NULL ? [NSString stringWithUTF8String:language] : @"en-US";
    dispatch_async(dispatch_get_main_queue(), ^{
        if (gSynth == nil)
        {
            gSynth = [[AVSpeechSynthesizer alloc] init];
            gSynth.usesApplicationAudioSession = YES;
        }
        if (gSynth.isSpeaking) [gSynth stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];
        AVSpeechUtterance *u = [AVSpeechUtterance speechUtteranceWithString:line];
        AVSpeechSynthesisVoice *voice = [AVSpeechSynthesisVoice voiceWithLanguage:lang];
        if (voice != nil) u.voice = voice;
        float lo = AVSpeechUtteranceMinimumSpeechRate, hi = AVSpeechUtteranceMaximumSpeechRate;
        u.rate = lo + (hi - lo) * fmaxf(0.f, fminf(1.f, rate));
        u.pitchMultiplier = fmaxf(0.5f, fminf(2.f, pitch));
        u.volume = fmaxf(0.f, fminf(1.f, volume));
        u.preUtteranceDelay = 0;
        [gSynth speakUtterance:u];
    });
}

void RetroSpeech_Stop(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (gSynth != nil && gSynth.isSpeaking) [gSynth stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];
    });
}

}
