# Bridge / ISL communication prototype

This is a responsive, dependency-free browser prototype for a two-way ISL communication app.

## Run it

From the workspace root, start a local web server:

```powershell
python -m http.server 8000
```

Then open `http://localhost:8000/ISL_Communication_App_Prototype/`. Camera and microphone access require localhost or HTTPS. Browser speech recognition support varies; some browsers process recognized speech through their own cloud service.

## Add a sign from a video

Run the extractor from the `ISL_MediaPipe` folder and provide a sign name:

```powershell
python extract_landmarks.py --input .\new-sign.mp4 --sign new-sign
```

After extraction succeeds, the script saves the JSON in the Unity project's `Assets/StreamingAssets/signs`, copies it into this prototype's `unity/StreamingAssets/signs`, and adds the sign to `app.js`'s `AVAILABLE_SIGNS` list. Reload the page to see it in the dropdown. If `--sign` is omitted, the extractor writes the generic `landmarks.json` and does not add a dropdown entry.

## What works in this interface

- Type a message and queue it for the future avatar player.
- Use browser speech recognition when the browser provides it.
- Read typed text or a transcript aloud with browser speech synthesis.
- Request camera access and show a live preview in the browser.
- Play available sign clips through the Unity WebGL avatar.
- Use a clearly labeled simulated transcript to demonstrate the return-flow controls.
- Resize down to mobile layouts.

## What is still a product / engineering task

- No sign-language recognition model is connected. Camera frames are previewed only, the sample transcript is simulated, and typed text uses direct sign-name lookup rather than full translation.
- Sign clips are played from the Unity WebGL StreamingAssets folder; newly extracted named signs are copied there and registered in the dropdown automatically.
- Speech/text to ISL needs ISL language planning and licensed, signer-reviewed content. Word-by-word English substitution is not sufficient.
- Camera-to-text needs a consented, representative signer dataset, robust continuous recognition, confidence handling and Deaf-led evaluation.

The interface makes those boundaries visible to avoid presenting an unvalidated system as a translator. Start by partnering with Deaf/ISL users and agreeing one narrow pilot setting, content scope and evaluation plan.
