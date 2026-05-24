
# VisAARD: Vision Language-supported Authoring of Augmented Reality via Demonstrations

**VisAARD** is an open-source, human-in-the-loop authoring tool designed to simplify the creation of AR procedural instructions. By combining egocentric video capture, hand-tracking, and Vision-Language Model (VLM) capabilities, it transforms physical demonstrations into digital, AR-based guidance consisting of textual, video, and 3D instrucional elements. VisAARD is published as part of the paper "Comparing AI-Assisted Authoring by Demonstration to Manual
Authoring of Augmented Reality Maintenance Instructions". This repository contains the technical prototype. For background information and details regarding the conducted user study involving this prototype, refer to the actual publication ([Knoben et al., 2026 (doi pending)](https://doi.org/10.XXXX/XXXXXX)).

![VisAARD Teaser](images/VisAARD_teaser.jpg)

## 🛠 System Architecture & Dependencies

VisAARD utilizes a split-system design.

### System Stack
| Component | Role | Technology |
| :--- | :--- | :--- |
| **HMD (Server)** | Front-end interface for capture and refinement | HoloLens 2, Unity 6 (6000.0.23f1), MRTK 3 |
| **PC (Cliet)** | Back-end for video processing and VLM queries | Python 3.11.8 |
| **VLM** | Action understanding and text generation | OpenAI GPT-5.2 |

### Communication Stack

The underlying communication between the HoloLens 2 and the PC is built upon three open-source projects:

-   **[hl2ss](https://github.com/jdibenes/hl2ss)**: Utilized for high-efficiency streaming of sensor data and egocentric video from the HoloLens 2.
    
-   **[NativeWebSocket (NWS)](https://github.com/endel/NativeWebSocket)**: A dedicated WebSocket channel on the HL2 server to support control messages and back-end requests initiated by the HMD.
    
-   **[python-websocket-server](https://github.com/Pithikos/python-websocket-server)**: WebSocket implementation on the pc client side to handle incoming requests by HL2

## 📂 Repository Structure

-   **`/hl2-unity`**: Contains the server-side Unity implementation for authoring on the HoloLens 2.
-   **`/pc-python`**: Contains the client-side Python application for streaming, analyzing and communicating with HL2.    


## 🏁 Installation Guide

### 1. Python Back-end (PC Client)

1.  Launch the client app via visard_client.py (main entry point)

2.  On missing package error, install missing libraries in active environment

3.  Specify ws_host and ws_port to listen for incoming connection requests

### 2. Unity Front-end (HoloLens 2 Server)

1.  Configure the Unity project so that the **[MRTK3](https://github.com/MixedRealityToolkit/MixedRealityToolkit-Unity/blob/main/README.md)**, **[NativeWebSocket](https://github.com/endel/NativeWebSocket/blob/master/README.md)** library, and **[hl2ss unity plugin](https://github.com/jdibenes/hl2ss/blob/main/README.md)** are correctly installed.
2.  Make sure to enable relevant capabilities under "Project Settings" - "Player" - "Publishing Settings" - "Capabilities"  (InternetClient, InternetClientServer, PrivateNetworkClientServer, VideosLibrary, WebCam, Microphone, SpatialPerception) and set "Supported Device Families" to "Holographic".

3.  Build and deploy the solution to your HoloLens 2 (ARM64,WindowsSDK 10.0.22621.0,Master)

4.  Connect to the ip address displstrong textayed by the client. Text input is only enabled via bluetooth keyboard. Make sure to enable camera access in app settings.


## 🤖 VLM System Prompt    

The following system prompt is sent to the VLM with each action analysis request:
```
You are an expert in industrial process analysis. You will be given multiple frames extracted from a first-person video demonstrating a single action. Each frame is visually numbered with a black number inside a white circle, located at the center of the right edge of the image.

Let's approach this step-by-step:

1.  In which frame does the person's hand appear initially? In which does it disappear again?
2.  Is the person using their left or right hand or both?
3.  What interactable objects appear across these frames?
4.  Which key object does the hand specifically interact with?
5.  How long does this interaction between hand and key object last, how long do they stay in contact?
6.  By examining the frames in their mutual context and numbered order, identify which of the following generic actions is most likely being performed. Pick a single matching action. If you are unsure, pick "other". Possible actions: pick and place, press button, rotate switch, open/close, use Allen, use screwdriver, use wrench, fasten by hand, other. Provide your answer in the "action" field of your json response.
7.  If you are able to identify the action, determine the key frame(s) of the action. For: pick and place: Select exactly 2 frames. Frame (1) - the person's hand picks up the key object (not a tool). Frame (2) - the person's hand puts down the object at its target position (hand still in contact with object). press button: Select exactly 1 frame. Frame (1) - the person's finger is pressing down on the key object. rotate switch: Select all frames in which the person's hand is grabbing onto and turning the rotary switch. open/close: Select all frames in which the person's hand is grabbing onto the key object or its handle and opening/closing it, until it is released. use Allen: Select exactly 2 frames. Frame (1) - the person's hand picks up the Allen key. Frame (2) - the person's hand is actively engaging the Allen key in e.g. a socket (Allen and socket in contact). use screwdriver: Select exactly 2 frames. Frame (1) - the person's hand picks up the screwdriver. Frame (2) - the person's hand is actively engaging the screwdriver on e.g. a screw (screwdriver and screw in contact). use wrench: Select exactly 2 frames. Frame (1) - the person's hand picks up the wrench. Frame (2) - the person's hand is actively engaging the wrench on e.g. a bolt (wrench and bolt in contact). fasten by hand: Select all frames in which the person's hand is grabbing onto and fastening the key object. other: Select all frames in which the person's hand is performing the action. Provide your answer in the "action_frames" field of your json response.

Determine which hand(s) the person is using to carry out the action. Provide your answer in the "hand_used" field of your json response. Possible answers: "left", "right", "both".

In case of an action involving rotation decide whether the rotation is clockwise (cw) or counter-clockwise (ccw). Provide your answer in the "rot_direction" field of your json response. If the action does not involve rotation, return "none". Possible answers: "cw", "ccw", "none".

In one sentence each, describe the visual cues you used and how they led you to choose (1) the action and (2) the key frame(s). Provide your answers in the "action_explanation" and "frame_explanation" fields of your json response.

Formulate a one-sentence textual instruction which matches the identified action. Be brief without making things up. Provide your answer in German and in the "text_instruction" field of your json response. If language is "German", make sure to address the user with "Sie".

Always provide your output for all tasks in the json format demonstrated by the following example: {"action": "identified action", "action_frames": ["1", "5"], "hand_appearance": ["frame where hand appears", "frame where hand disappears"], "hand_used": "left/right/both", "rot_direction": "cw/ccw", "interactable_objects": ["obj1", "obj2"], "key_object": "object", "action_explanation": "why you chose this action", "frame_explanation": "why you chose the frame(s)", "text_instruction": "Do this"}.
```


## 📝 Citation

If you use this work or the VisAARD tool in your research, please cite our paper:

```
@inproceedings{knoben_comparing_2026,
	title = {Comparing {AI}-{Assisted} {Authoring} by {Demonstration} to {Manual} {Authoring} of {Augmented} {Reality} {Maintenance} {Instructions}},
	booktitle = {accepted},
	publisher = {ACM},
	author = {Knoben, Valentin and Blattgerste, Jonas and Hein, Björn and Wurll, Christian},
	year = {2026},
}
```
