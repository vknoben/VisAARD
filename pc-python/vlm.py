from pyexpat import model
from tkinter import filedialog
import openai
import os
import base64
import cv2
from threading import Lock, Event
import json
import wss
import language
from google import genai
from google.genai import types
import time
from pydantic import BaseModel
from typing import List

# Shared vars -------------------------------------------------------
text_lock = Lock()
textual_instruction = ""

gpt_result_available = Event()
gpt_result_available.clear()
gpt_lock = Lock()                   # For display in gui and cross thread data access
gpt_action = ""
gpt_frames = []
gpt_hand_appearance = []
gpt_hand_used = ""
gpt_rot_direction = ""
gpt_objects = []
gpt_key_object = ""
gpt_action_expl = ""
gpt_frame_expl = ""
gpt_gen_text = ""
gpt_response_time = ""

gemini_result_available = Event()
gemini_result_available.clear()
gemini_lock = Lock()                 
gemini_action = ""
gemini_frame = ""
gemini_explanation = ""

# Prompts --------------------------------------------------------------
en_multi_frame = """
    You are an expert in industrial process analysis.
    You will be given multiple frames extracted from a first-person video demonstrating a single action and a textual instruction describing this action. Each frame is visually numbered with a black number inside a white circle, located at the center of the right edge of the image.
    Let's approach this step-by-step:
    1. In which frame does the person's hand appear initially? In which does it disappear again?
    2. Is the person using their left or right hand or both?
    3. What interactable objects appear across these frames?
    4. Which key object does the hand specifically interact with?
    5. How long does this interaction between hand and key object last, how long do they stay in contact?

    1. By examining the frames in their mutual context and numbered order, identify which of the following generic actions is most likely being performed. Pick a single matching action. In cases of ambiguity between textual instruction and visual frames, lean more towards visual evidence. If you are unsure, do not pick an action. Possible actions:
        - pick and place
        - press
        - rotate
        - pull/push
    Provide your answer in the "action" field of your json response. 

    2. If you are able to identify the action, determine the key frame(s) of the action. For:
        - pick and place: Select one frame each in which the person is (1) picking up the key object and (2) putting it down at its target location.
        - press: Select one frame in which the person is (1) pressing down on the control element.
        - rotate: Select all frames in which the person is (1) grabbing onto and turning the control element.
        - pull/push: Select all frames in which the person is (1) grabbing onto the key object or its handle and pulling or pushing it.
    Always make sure that the frames you select actually show moments where the user's hand is in clear contact with the key object.
    Provide your answer in the "action_frames" field of your json response.

    3. Determine which hand(s) the person is using to carry out the action. Provide your answer in the "hand_used" field of your json response. Possible answers: "left", "right", "both". 

    4. In case of an action involving rotation, consider the textual instruction and the visual frames depicting the rotation and decide whether the rotation is clockwise (cw) or counter-clockwise (ccw). Provide your answer in the "rot_direction" field of your json response. If the action does not involve rotation, return "none". Possible answers: "cw", "ccw", "none".

    5. In one sentence each, describe the visual cues you used and how they led you to choose (1) the action and (2) the key frame(s). Provide your answers in the "action_explanation" and "frame_explanation" fields of your json response.

    Always provide your output for all tasks in the json format demonstrated by the following example: {"action": "identified action", "action_frames": ["1", "5"], "hand_appearance": ["frame where hand appears", "frame where hand disappears"], "hand_used": "left/right/both", "rot_direction": "cw/ccw", "interactable_objects": ["obj1", "obj2"], "key_object": "object", "action_explanation": "why you chose this action", "frame_explanation": "why you chose the frame(s)"}. 
    If you are unable to identify a matching action, return a json with empty fields: {"action": "", "action_frames": [], "hand_appearance": [], "hand_used": "", "rot_direction": "", "interactable_objects": [], "key_object": "", "action_explanation": "", "frame_explanation": ""}.
"""

en_multi_frame_img_only_text_gen = """
    You are an expert in industrial process analysis.
    You will be given multiple frames extracted from a first-person video demonstrating a single action. Each frame is visually numbered with a black number inside a white circle, located at the center of the right edge of the image.
    Let's approach this step-by-step:
    1. In which frame does the person's hand appear initially? In which does it disappear again?
    2. Is the person using their left or right hand or both?
    3. What interactable objects appear across these frames?
    4. Which key object does the hand specifically interact with?
    5. How long does this interaction between hand and key object last, how long do they stay in contact?

    1. By examining the frames in their mutual context and numbered order, identify which of the following generic actions is most likely being performed. Pick a single matching action. If you are unsure, do not pick an action. Possible actions: pick and place, press button, rotate switch, open/close, use Allen.
       
    Provide your answer in the "action" field of your json response. 

    2. If you are able to identify the action, determine the key frame(s) of the action. For:
        - pick and place: Select exactly 2 frames. Frame (1) - the person's hand picks up the key object. Frame (2) - the person's hand puts down the object at its target position (hand still in contact with object).
        - press button: Select exactly 1 frame. Frame (1) - the person's finger is pressing down on the key object.
        - rotate switch: Select a range of frames in which the person's hand is grabbing onto and turning the rotary switch.
        - open/close: Select a range of frames in which the person's hand is grabbing onto the key object or its handle and opening/closing it, until it is released.
        - use Allen: Select exactly two frames. Frame (1) - the person's hand picks up the Allen key. Frame (2) - the person's hand is actively engaging the Allen key in a socket (Allen and socket in contact).
    Provide your answer in the "action_frames" field of your json response.

    3. Determine which hand(s) the person is using to carry out the action. Provide your answer in the "hand_used" field of your json response. Possible answers: "left", "right", "both". 

    4. In case of an action involving Rotation decide whether the rotation is clockwise (cw) or counter-clockwise (ccw). Provide your answer in the "rot_direction" field of your json response. If the action does not involve rotation, return "none". Possible answers: "cw", "ccw", "none".

    5. In one sentence each, describe the visual cues you used and how they led you to choose (1) the action and (2) the key frame(s). Provide your answers in the "action_explanation" and "frame_explanation" fields of your json response.

    6. Formulate a one-sentence textual instruction which matches the identified action. Be as specific but brief as possible without making things up. Do not specify which hand was used or which direction to rotate. Provide your answer in German and in the "text_instruction" field of your json response.

    Always provide your output for all tasks in the json format demonstrated by the following example: {"action": "identified action", "action_frames": ["1", "5"], "hand_appearance": ["frame where hand appears", "frame where hand disappears"], "hand_used": "left/right/both", "rot_direction": "cw/ccw", "interactable_objects": ["obj1", "obj2"], "key_object": "object", "action_explanation": "why you chose this action", "frame_explanation": "why you chose the frame(s)", "text_instruction": "Do this"}. 
    If you are unable to identify a matching action, return a json with empty fields: {"action": "", "action_frames": [], "hand_appearance": [], "hand_used": "", "rot_direction": "", "interactable_objects": [], "key_object": "", "action_explanation": "", "frame_explanation": "", "text_instruction": ""}.
    """

en_multi_frame_tm_short = """
    You are an expert in industrial process analysis. 
    You will be given multiple frames extracted from a first-person video demonstrating a single action and a textual instruction describing this action. Each frame is visually numbered with a black number inside a white circle, located at the center of the right edge of the image.
    Let's approach this step-by-step:
    1. What interactable objects appear in these frames?
    2. Is the person using their left or right hand or both hands to perform the action?
    3. In which frame does the person's hand appear initially? In which does it disappear again?
    4. What object does the hand interact with?
    Task 1: Based on your analysis, identify which of the following actions is most likely being performed. Pick a single matching action. If you are unsure, do not pick an action. Possible actions are:
    - pick and place
    - use screwdriver
    - use Allen
    - use wrench
    - press button
    - rotate switch
    - flip toggle
    - fasten by hand
    Task 2: If you are able to identify the action, determine the key frame(s) where the action occurs. For:
    - pick and place: Select the respective frames in which the person (1) grabs the object and (2) places the object in its final position.
    - use screwdriver: Select the frame in which the person (1) picks up the tool and (2) engages the screwdriver and starts turning it.
    - use Allen: Select the frame in which the person (1) picks up the tool and (2) engages the Allen key and starts rotating it.
    - use wrench: Select the frame in which the person (1) picks up the tool and (2) engages the wrench on a bolt or nut and begins turning it.
    - press button: Select the frame in which the person (1) presses the button.
    - rotate switch: Select the frame in which the person (1) starts turning the rotational switch.
    - flip toggle: Select the frame in which the person (1) flips the toggle switch. 
    - fasten by hand: Select the frame in which the person (1) starts fastening the object by hand.
    Task 3: Determine which hand(s) the person is using to carry out the action. Possible answers are: left, right, both.
    Always provide your output for all tasks in the json format demonstrated by the following example: {"action": "identified action", "action_frames": ["1", "2"], "hands": "left/right/both"}. 
    If you are unable to identify a matching action, return a json with empty fields: {"action": "", "action_frames": [], "hands": ""}
"""

de_multi_frame = """
"""

de_multi_frame_short = """
"""

# Json Schemata ----------------------------------------------------
chatgpt_json_schema = {
    "type": "object",
    "properties": {
      "action": {
        "type": "string",
        "description": "The identified action being performed."
      },
      "action_frames": {
        "type": "array",
        "description": "Set of frames associated with the action.",
        "items": {
          "type": "string"
        }
      },
      "hand_appearance": {
        "type": "array",
        "description": "Frames where the hand appears and disappears.",
        "items": {
          "type": "string"
        }
      },
      "hand_used": {
        "type": "string",
        "description": "Indicates if the left, right, or both hands are used."
      },
      "rot_direction": {
        "type": "string",
        "description": "Indicates direction of rotation in case or rotary action (cw/ccw)."
      },
      "interactable_objects": {
        "type": "array",
        "description": "List of objects that can be interacted with during the action.",
        "items": {
          "type": "string"
        }
      },
      "key_object": {
        "type": "string",
        "description": "The main object involved in the action."
      },
      "action_explanation": {
        "type": "string",
        "description": "Explanation for why the identified action was chosen."
      },
      "frame_explanation": {
        "type": "string",
        "description": "Explanation for why the specific frame(s) were chosen."
      },
      "text_instruction": {
          "type": "string",
          "description": "A one-sentence textual instruction matching the identified action, formulated in German."
      }
    },
    "required": [
      "action",
      "action_frames",
      "hand_appearance",
      "hand_used",
      "rot_direction",
      "interactable_objects",
      "key_object",
      "action_explanation",
      "frame_explanation",
      "text_instruction"
    ],
    "additionalProperties": False
}

# Json schema for responses api
class GptResponseSchema(BaseModel):
    action: str
    action_frames: List[str]
    hand_appearance: List[str]
    hand_used: str
    rot_direction: str
    interactable_objects: List[str]
    key_object: str
    action_explanation: str
    frame_explanation: str
    text_instruction: str

chatgpt_json_schema_short = {
"type": "object",
    "properties": {
          "action": {
                "type": "string",
                "description": "The identified action."
          },
          "action_frames": {
              "type": "array",
              "items": {
                  "type": "string"
              },
              "description": "The key frame(s) in which the action occurs."
          },
          "hands": {
              "type": "string",
              "description": "Hand(s) used to carry out the action."
          }
    },
    "additionalProperties": False,
    "required": [
        "action",
        "action_frames",
        "hands"
    ]
}

# Methods -----------------------------------------------------------
def get_current_prompt(actions):
    """
    Creates prompt based on relevant actions

    Args:
        actions: string container of relevant actions. Can change e.g. if misdetection and action is removed

    Returns:
        string: string prompt
    """

    tasks = {
        "pick and place":  "Select exactly 2 frames. Frame (1) - the person's hand picks up the key object (not a tool). Frame (2) - the person's hand puts down the object at its target position (hand still in contact with object).",
        "press button":    "Select exactly 1 frame. Frame (1) - the person's finger is pressing down on the key object.",
        "rotate switch":   "Select all frames in which the person's hand is grabbing onto and turning the rotary switch.",
        "open/close":      "Select all frames in which the person's hand is grabbing onto the key object or its handle and opening/closing it, until it is released.",
        "use Allen":       "Select exactly 2 frames. Frame (1) - the person's hand picks up the Allen key. Frame (2) - the person's hand is actively engaging the Allen key in e.g. a socket (Allen and socket in contact).",
        "use wrench":      "Select exactly 2 frames. Frame (1) - the person's hand picks up the wrench. Frame (2) - the person's hand is actively engaging the wrench on e.g. a bolt (wrench and bolt in contact).",
        "use screwdriver": "Select exactly 2 frames. Frame (1) - the person's hand picks up the screwdriver. Frame (2) - the person's hand is actively engaging the screwdriver on e.g. a screw (screwdriver and screw in contact).",
        "fasten by hand":  "Select all frames in which the person's hand is grabbing onto and fastening the key object.",
        "other":           "Select all frames in which the person's hand is performing the action."
        }

    relevant_actions = ", ".join(actions)

    relevant_tasks = [a for a in actions if a in tasks]

    relevant_tasks = "\n".join([f"- {a}: {tasks[a]}" for a in relevant_tasks])
    # relevant_tasks = "\n".join([f"- {tasks[a]}" for a in relevant_tasks])

    language_str = "German" if language.current_language == 0 else "English"

    prompt = f"""
    You are an expert in industrial process analysis.
    You will be given multiple frames extracted from a first-person video demonstrating a single action. Each frame is visually numbered with a black number inside a white circle, located at the center of the right edge of the image.
    Let's approach this step-by-step:
    1. In which frame does the person's hand appear initially? In which does it disappear again?
    2. Is the person using their left or right hand or both?
    3. What interactable objects appear across these frames?
    4. Which key object does the hand specifically interact with?
    5. How long does this interaction between hand and key object last, how long do they stay in contact?

    1. By examining the frames in their mutual context and numbered order, identify which of the following generic actions is most likely being performed. Pick a single matching action. If you are unsure, pick "other". Possible actions: {relevant_actions}.
    Provide your answer in the "action" field of your json response. 

    2. If you are able to identify the action, determine the key frame(s) of the action. For:
    {relevant_tasks}
    Provide your answer in the "action_frames" field of your json response.

    3. Determine which hand(s) the person is using to carry out the action. Provide your answer in the "hand_used" field of your json response. Possible answers: "left", "right", "both". 

    4. In case of an action involving Rotation decide whether the rotation is clockwise (cw) or counter-clockwise (ccw). Provide your answer in the "rot_direction" field of your json response. If the action does not involve rotation, return "none". Possible answers: "cw", "ccw", "none".

    5. In one sentence each, describe the visual cues you used and how they led you to choose (1) the action and (2) the key frame(s). Provide your answers in the "action_explanation" and "frame_explanation" fields of your json response.

    6. Formulate a one-sentence textual instruction which matches the identified action. Be brief without making things up. Provide your answer in {language_str} and in the "text_instruction" field of your json response. If language is "German", make sure to address the user with "Sie".

    Always provide your output for all tasks in the json format demonstrated by the following example: {{"action": "identified action", "action_frames": ["1", "5"], "hand_appearance": ["frame where hand appears", "frame where hand disappears"], "hand_used": "left/right/both", "rot_direction": "cw/ccw", "interactable_objects": ["obj1", "obj2"], "key_object": "object", "action_explanation": "why you chose this action", "frame_explanation": "why you chose the frame(s)", "text_instruction": "Do this"}}.
    """

    return prompt

def convert_to_base64(image):
    """
    Converts an image (opencv numpy array) to base64 format for image processing by VLM
    
    Args:
        image: The image to be encoded as base64
        
    Returns:
        image: The image encoded as base64
    """
    _, buffer = cv2.imencode(".jpg", image)
    image = base64.b64encode(buffer).decode("utf-8")
    
    return image

def request_gpt_multi_frame(images, english, short, relevant_actions):
    """
    Performs request to openai API with multiple frames

    Args:
        images: Sampled frames from video instruction for this task step
        english: Whether to make request using English prompt. If False -> German
        short: Whether to use short prompt, only action and frame(s). If False -> long prompt with additional information

    Returns:
        json of api response
    """
    
    # Get api key from environment vars
    openai.api_key = os.getenv("OPENAI_API_KEY")
    client = openai.OpenAI()

    # Specify message content
    # System prompt (language sensitive)
    # if english:
    #     prompt = en_multi_frame_tm_short if short else en_multi_frame
    # else:
    #     prompt = de_multi_frame_short if short else de_multi_frame
    # Use english prompt by default
    # prompt = en_multi_frame_img_only_text_gen

    # messages = [{"role": "developer", "content": prompt}]

    # Encode images as base_64 and add to api request
    # for img in images:
        # messages.append({
        #     "role": "user",
        #     "content": [{
        #         "type": "image_url",
        #         "image_url": {"url": f"data:image/jpg;base64,{convert_to_base64(img)}", "detail": "high"}
        #     }]
        # })  

    # Build prompt based on specified relevant actions
    prompt = get_current_prompt(relevant_actions)

    # Default system message
    prompt_msg = {
        "role": "developer",
        "content": [
            {
                "type": "input_text",
                "text": prompt
            }
        ]
    }

    # User message with images
    # User message with multiple images
    user_msg = {
        "role": "user",
        "content": []
    }


    for img in images:
        user_msg["content"].append({
            "type": "input_image",
            "image_url": f"data:image/jpeg;base64,{convert_to_base64(img)}",
            "detail": "high"
        }) 

    # Measure time
    start_time = time.time()

    # Make request
    try:
        
        print("Made request to ChatGPT...")

        response = client.responses.parse(
            model="gpt-5-mini",
            input=[prompt_msg, user_msg],
            text_format=GptResponseSchema,
            text={"verbosity": "low"},
            reasoning={"effort": "low"}
        )

        # response = client.responses.parse(
        # model="gpt-4.1",
        # temperature = 0.3,
        # input=[prompt_msg, user_msg],
        # text_format=GptResponseSchema
        # )

        print("Received response from ChatGPT")
    except Exception as e:
        print(f"Failed to request ChatGPT due to: {e}")
    
    # Calculate process duration
    end_time = time.time()
    process_duration = end_time - start_time
    # print(f"Total duration: ~{process_duration:.2f}s")
    
    # Load response string as json
    try:
        # result = json.loads(response.choices[0].message.content)

        result = response.output_parsed
        print(result)  

    except Exception as e:
        print(f"Failed to deserialize response by ChatGPT with error: {e}")
        return

    with gpt_lock:        
        # Store result (for display in gui)
        global gpt_action, gpt_frames, gpt_response_time, gpt_hand_used, gpt_rot_direction, gpt_gen_text
        # gpt_action = result["action"]
        # gpt_frames = result["action_frames"]
        # # gpt_hand_appearance = result["hand_appearance"]
        # gpt_hand_used = result["hand_used"]
        # gpt_rot_direction = result["rot_direction"]
        # # gpt_objects = result["interactable_objects"]
        # # gpt_key_object = result["key_object"]
        # # gpt_action_expl = result["action_explanation"]
        # # gpt_frame_expl = result["frame_explanation"]
        # gpt_gen_text = result["text_instruction"]
        # gpt_response_time = f"{process_duration:.2f}"

        gpt_action = result.action
        gpt_frames = result.action_frames
        gpt_hand_used = result.hand_used
        gpt_rot_direction = result.rot_direction
        gpt_gen_text = result.text_instruction
        gpt_response_time = f"{process_duration:.2f}"
        
        # Inform HL2 about result
        # wss.send_msg(wss.MsgType.GPTRESULT, response.choices[0].message.content)
        wss.send_msg(wss.MsgType.GPTRESULT, result.json())

    # Signal gui that new analysis result is in
    gpt_result_available.set()

def request_gemini(image):
    """
        Performs request to google gemini API

    Args:
         image: Grid image (path) of video instruction for this task step

    Returns:
          json of api response
    """
    
    print("Requesting analysis from Gemini")

    # Choose english or german prompt depending on currently set language
    prompt = de_prompt if language.current_language == 0 else en_prompt 

    # Get api key form environment variables
    client = genai.Client(api_key=os.environ.get("GOOGLE_API_KEY"))

    # Specify model, content, and request configurations
    model = "gemini-2.0-flash"
    contents = [textual_instruction, image]
    generate_content_config = types.GenerateContentConfig(
        temperature=0.3,
        top_p=0.8,
        top_k=32,
        max_output_tokens=8192,
        response_mime_type="application/json",
        response_schema=genai.types.Schema(
            type = genai.types.Type.OBJECT,
            required = ["action", "action_frame", "explanation"],
            properties = {
                "action": genai.types.Schema(
                    type = genai.types.Type.STRING,
                ),
                "action_frame": genai.types.Schema(
                    type = genai.types.Type.STRING,
                ),
                "explanation": genai.types.Schema(
                    type = genai.types.Type.STRING,
                ),
            },
        ),
        system_instruction=[prompt],
    )
    
    # Request api
    response = client.models.generate_content(
        model=model,
        contents=contents,
        config=generate_content_config,
    )

    # Load response string as json
    json_response = json.loads(response.text)
    
    with gemini_lock:        
        # Store result
        global gemini_action, gemini_frame, gemini_explanation
        gemini_action = json_response["action"]
        gemini_frame = json_response["action_frame"]
        gemini_explanation = json_response["explanation"]
        
        # Inform HL2 about result
        wss.send_msg(wss.MsgType.GEMINIRESULT, response.text)

    # Signal gui that new analysis result is in
    gemini_result_available.set()
    print(f"Response from Gemini{response.text}")

# Debug
# frame_paths = filedialog.askopenfilenames(title="Select Images", filetypes=[("Image files", "*.jpg *.jpeg *.png *.bmp *.tiff")])
# frames = []
# for path in frame_paths:
#     frames.append(cv2.imread(path))
# request_gpt_multi_frame(frames, True, False, ["pick and place", "press button", "rotate switch", "open/close", "use Allen"])
# current_language = 1
# print(get_current_prompt(["pick and place", "press button", "rotate switch", "open/close", "use Allen"]))