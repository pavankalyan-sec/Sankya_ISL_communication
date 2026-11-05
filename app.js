const byId = (id) => document.getElementById(id);
const messageInput = byId('messageInput');
const transcriptOutput = byId('transcriptOutput');
const speechStatus = byId('speechStatus');
const video = byId('cameraVideo');
let cameraStream = null;
let recognition = null;
let unityInstance = null;

// =============================================================
// AVAILABLE SIGNS
// Maps directly to StreamingAssets/signs/<id>.json
// =============================================================
const AVAILABLE_SIGNS = [
  { id: 'app',        label: 'App' },
  { id: 'apple',      label: 'Apple' },
  { id: 'bag',        label: 'Bag' },
  { id: 'editor',     label: 'Editor' },
  { id: 'effort',     label: 'Effort' },
  { id: 'hello',  label: 'Hello' },
  { id: 'independent',label: 'Independent' },
  { id: 'love',       label: 'Love' },
  { id: 'no',         label: 'No' },
  { id: 'passport',   label: 'Passport' },
  { id: 't-shirt',    label: 'T-Shirt' }
];

// =============================================================
// UNITY WEBGL LOADER
// Points to the build artifacts inside unity/Build
// =============================================================
(function loadUnity() {
  const buildUrl = 'unity/Build';
  const prefix = 'ISL_Communication_App_Prototype';

  const config = {
    dataUrl: `${buildUrl}/${prefix}.data`,
    frameworkUrl: `${buildUrl}/${prefix}.framework.js`,
    codeUrl: `${buildUrl}/${prefix}.wasm`,
    streamingAssetsUrl: 'unity/StreamingAssets',
    companyName: 'ISL',
    productName: 'ISL_Translator',
    productVersion: '1.0'
  };

  const canvas = byId('unity-canvas');
  const holder = byId('unityHolder');
  const loader = byId('unityLoading');
  const placeholder = byId('avatarPlaceholder');

  const script = document.createElement('script');
  script.src = `${buildUrl}/${prefix}.loader.js`;

  script.onload = () => {
    if (typeof createUnityInstance !== 'function') {
      loader.textContent = 'Unity loader not found.';
      return;
    }

    holder.hidden = false;
    if (placeholder) placeholder.hidden = true;

    createUnityInstance(canvas, config, (progress) => {
      loader.textContent = `Loading Unity avatar… ${Math.round(progress * 100)}%`;
    }).then((instance) => {
      unityInstance = instance;
      loader.style.display = 'none';
      byId('avatarCaption').textContent = 'Avatar ready';
      byId('motionSlot').textContent = 'Select a sign and press Play.';
      byId('replayButton').disabled = false;
    }).catch((err) => {
      loader.textContent = `Unity failed to load: ${err.message}`;
      console.error('Unity load error:', err);
    });
  };

  script.onerror = () => {
    console.warn(`Unity loader script failed to load from ${script.src}`);
    byId('avatarCaption').textContent = 'Unity loader failed';
    byId('motionSlot').textContent = 'Check that Build files are present.';
  };

  document.body.appendChild(script);
})();

// =============================================================
// SIGN SELECTOR
// =============================================================
(function populateSignSelector() {
  const select = byId('signSelect');
  const status = byId('signStatus');

  select.innerHTML = '';

  if (AVAILABLE_SIGNS.length === 0) {
    select.innerHTML = '<option value="">No signs available</option>';
    status.textContent = 'No signs have been added yet.';
    return;
  }

  AVAILABLE_SIGNS.forEach((sign) => {
    const opt = document.createElement('option');
    opt.value = sign.id;
    opt.textContent = sign.label;
    select.appendChild(opt);
  });

  select.disabled = false;
  byId('playSignButton').disabled = false;
  status.textContent = `${AVAILABLE_SIGNS.length} signs ready. Select one and press Play.`;
})();

// =============================================================
// PLAY SIGN (Calls ISLSignBridge in Unity)
// =============================================================
function playSelectedSign() {
  const signId = byId('signSelect').value;
  if (!signId) return;

  const label = byId('signSelect').options[byId('signSelect').selectedIndex].text;

  if (!unityInstance) {
    byId('signStatus').textContent = 'Avatar is still loading — please wait.';
    return;
  }

  // Sends message to the SignPlaybackBridge receiver GameObject
  unityInstance.SendMessage('ISLSignBridge', 'PlaySign', signId);

  byId('avatarMessage').textContent = `Playing: ${label}`;
  byId('avatarCaption').textContent = 'Playing sign…';
  byId('motionSlot').textContent = `Playing "${label}" sign clip.`;
  byId('clipStatus').textContent = `"${label}" clip sent to avatar.`;
  byId('signStatus').textContent = `Playing "${label}".`;
  byId('replayButton').disabled = false;
  byId('avatarCard').scrollIntoView?.({ behavior: 'smooth', block: 'nearest' });
}

byId('playSignButton').addEventListener('click', playSelectedSign);

// =============================================================
// SEND BUTTON (Text -> Sign lookup)
// =============================================================
byId('sendButton').addEventListener('click', () => {
  const message = messageInput.value.trim().toLowerCase();
  if (!message) {
    messageInput.focus();
    speechStatus.textContent = 'Type or dictate a message first.';
    return;
  }

  const match = AVAILABLE_SIGNS.find(
    (s) => s.id === message || s.label.toLowerCase() === message
  );

  if (match) {
    byId('signSelect').value = match.id;
    playSelectedSign();
    speechStatus.textContent = `Found sign "${match.label}" — playing now.`;
  } else {
    byId('avatarMessage').textContent = `Message: "${messageInput.value.trim()}"`;
    byId('avatarCaption').textContent = 'Sign not found';
    byId('motionSlot').textContent = `No sign clip found for "${message}".`;
    byId('clipStatus').textContent = 'No sign clip is mapped to this message.';
    speechStatus.textContent = `No saved sign matches "${message}". Available: ${AVAILABLE_SIGNS.map((s) => s.label).join(', ')}.`;
    byId('avatarCard').scrollIntoView?.({ behavior: 'smooth', block: 'nearest' });
  }
});

// =============================================================
// CHARACTER COUNTER
// =============================================================
messageInput.addEventListener('input', () => {
  byId('charCount').textContent = `${messageInput.value.length} / 500`;
});

// =============================================================
// SPEECH OUTPUT
// =============================================================
byId('listenButton').addEventListener('click', () => speak(messageInput.value, speechStatus));
byId('speakResultButton').addEventListener('click', () => speak(transcriptOutput.value, byId('transcriptStatus')));

function speak(text, status) {
  if (!text.trim()) {
    status.textContent = 'Enter text before using speech output.';
    return;
  }
  if (!('speechSynthesis' in window)) {
    status.textContent = 'Speech output is not supported in this browser.';
    return;
  }
  window.speechSynthesis.cancel();
  const utterance = new SpeechSynthesisUtterance(text.trim());
  utterance.lang = 'en-IN';
  utterance.onstart = () => { status.textContent = 'Speaking in English with your browser voice.'; };
  utterance.onend = () => { status.textContent = 'Speech finished.'; };
  utterance.onerror = () => { status.textContent = 'Speech could not be played.'; };
  window.speechSynthesis.speak(utterance);
}

// =============================================================
// VOICE INPUT
// =============================================================
byId('micButton').addEventListener('click', () => {
  const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
  if (!SpeechRecognition) {
    speechStatus.textContent = 'Voice input is not available here. Type your message instead.';
    return;
  }
  if (recognition) {
    recognition.stop();
    return;
  }
  recognition = new SpeechRecognition();
  recognition.lang = 'en-IN';
  recognition.interimResults = true;
  recognition.continuous = false;
  byId('micButton').textContent = 'Listening...';
  speechStatus.textContent = 'Listening…';
  recognition.onresult = (event) => {
    let result = '';
    for (let i = event.resultIndex; i < event.results.length; i++) result += event.results[i][0].transcript;
    messageInput.value = result;
    byId('charCount').textContent = `${result.length} / 500`;
  };
  recognition.onerror = (event) => {
    speechStatus.textContent = event.error === 'not-allowed' ? 'Microphone access blocked.' : `Voice input stopped: ${event.error}.`;
  };
  recognition.onend = () => {
    byId('micButton').innerHTML = '<span aria-hidden="true">●</span> Use voice';
    recognition = null;
  };
  recognition.start();
});

// =============================================================
// CAMERA PREVIEW
// =============================================================
byId('cameraButton').addEventListener('click', async () => {
  if (cameraStream) {
    stopCamera();
    return;
  }
  if (!navigator.mediaDevices?.getUserMedia) {
    byId('cameraLabel').textContent = 'Camera unavailable';
    return;
  }
  try {
    cameraStream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user' }, audio: false });
    video.srcObject = cameraStream;
    video.hidden = false;
    byId('cameraEmpty').hidden = true;
    byId('cameraLabel').textContent = 'Camera preview only';
    byId('cameraButton').textContent = 'Stop camera';
  } catch (error) {
    byId('cameraLabel').textContent = error.name === 'NotAllowedError' ? 'Camera permission denied' : 'Camera unavailable';
  }
});

function stopCamera() {
  cameraStream?.getTracks().forEach((track) => track.stop());
  cameraStream = null;
  video.srcObject = null;
  video.hidden = true;
  byId('cameraEmpty').hidden = false;
  byId('cameraLabel').textContent = 'Camera off';
  byId('cameraButton').textContent = 'Start camera';
}

// =============================================================
// REPLAY / SPEED
// =============================================================
byId('replayButton').addEventListener('click', () => {
  if (!unityInstance) return;
  const signId = byId('signSelect').value;
  if (signId) {
    unityInstance.SendMessage('ISLSignBridge', 'PlaySign', signId);
    byId('clipStatus').textContent = 'Replaying…';
  }
});

byId('speedControl').addEventListener('change', () => {
  if (!unityInstance) return;
  const speed = parseFloat(byId('speedControl').value);
  if (!isNaN(speed)) {
    unityInstance.SendMessage('ISLSignBridge', 'SetPlaybackSpeed', speed.toString());
  }
});

byId('sampleResultButton').addEventListener('click', () => {
  transcriptOutput.value = 'I need help.';
  byId('transcriptStatus').textContent = 'SIMULATED SAMPLE';
});
byId('dismissNotice').addEventListener('click', () => byId('dismissNotice').parentElement.remove());
byId('aboutButton').addEventListener('click', () => byId('aboutDialog').showModal());
byId('aboutDialog').addEventListener('click', (event) => {
  if (event.target === byId('aboutDialog')) byId('aboutDialog').close();
});
window.addEventListener('pagehide', stopCamera);
