from xml.etree.ElementInclude import DEFAULT_MAX_INCLUSION_DEPTH
from websocket_server import WebsocketServer
# See https://github.com/Pithikos/python-websocket-server for documentation

from threading import Event, Thread
from enum import Enum
from dataclasses import dataclass
import json
import os

from streaming import connect_stream, user_is_capturing, hl2ss_connected, sample_interval, assisted_mode, process_frames, frame_counter
import vlm
from video_analyzer import prepared_frames, grid_from_prepared_frames, extract_frames_with_hands, reanalyze_frames
from study_logger import logger

# Shared vars -----------------------------------------------------------------
connected = Event()							# Indicates whether ws connection is established
connected.clear()   
connect_state_changed = Event()				# Indicates change in connection state
connect_state_changed.clear()

mode_changed = Event()						# Usage mode changed (assisted, manual, guidance)
mode_changed.clear()
assisted_auth_mode = Event()				# Assisted?
assisted_auth_mode.clear()
manual_auth_mode = Event()					# Manual?
manual_auth_mode.clear()
guidance_mode = Event()						# Guidance?
guidance_mode.clear()
new_text_instruction_available = Event()	# Received new text instruction from hl2
new_text_instruction_available.clear()
moved_to_next_step = Event()				# User continues authoring next step
moved_to_next_step.clear()
received_used_hand = Event()				# HL2 informed client about used hand (meaning server disagrees with client)
received_used_hand.clear()
received_rot_direction = Event()			# HL2 informed client about rotation direction
received_rot_direction.clear()

ws_server = None # Server object
ws_host = "0.0.0.0"
ws_port = 3001

current_wf_name = ""		# Name of currently authored workflow
current_step = "1"			# Current step number (starts at 1 -> first step)

hand_used_server = ""		# Hand used as determined by server (hl2)
rot_direction_server = ""	# Rotation direction as determined by server (hl2)

# Class vars ------------------------------------------------------------------
hl_ipaddress = "0.0.0.0" # Will be updated based on message from hl2

# All possible message types (this list needs to be synced with hl2)
class MsgType(Enum):
	DEBUG = 0						# "debug message" (text)
	IPADDRESS = 1					# "123.234.345.456" (ip address)
	WFNAME = 2						# "workflow1" (name of workflow)
	NEWSTEP = 3						# "3" (step number)
	TEXTINSTRUCT = 4				# "do this" (textual instruction)
	CAPTURESTART = 5				# "" (empty)
	CAPTUREEND = 6					# "" (empty)
	GPTRESULT = 7					# "{"action": "pick and place", "action_frame": "3"} (json)
	GEMINIRESULT = 8				# "{"action": "pick and place", "action_frame": "3"} (json)
	AUTHFINISH = 9					# "" (empty, end of complete authoring process)
	MODE = 10						# "manual", "assisted" for authoring or "guidance" (mode as string)
	GUIDEFINISH = 11				# "" (empty)
	SAMPLERATE = 12					# "15" (sample interval)
	HANDUSED = 13					# "left/right" (hand used determined by handtracking data)
	VIDEOCONFIRMED = 14				# "" (empty, user confirmed final video capture)
	ROTDIRECTION = 15				# "cw/ccw" (rotation direction in case of rotary action)
	TRIMLENGTH = 16					# "{"from_start": "1.2", "from_end": "2.3"} (trim lengths from start and end)
	REGENERATE = 17					# "{"step_number": "3", "false_action": "press_button"}" (json)
	AUTHSTART = 18					# "" (empty, start of complete authoring process)
	NEWSTEPINSITU = 19				# "3" (step number for which in-situ instructions are now displayed to user)
	AUTHMODAL = 20					# "text/video/3d" (user chose to author text, video, or 3D in manual mode)
	AUTHMODALFINISH = 21			# "" (empty, user finished authoring single instruction modality in manual mode)
	DIDMANIPULATE = 22				# "" (empty, user manipulated 3D instruction in assisted in-situ authoring)
	DIDEDITTEXT = 23				# "" (empty, user edited text in asssted in-situ authoring)
	DIDARRANGE = 24					# "" (empty, user arranged instruction panel either in assisted or manual authoring)

# Serializable message class --------------------------------------------------
@dataclass
class Message:
    msg_type: MsgType
    message: str
	# Convert Message object to JSON string
    def to_json(self) -> str:
        return json.dumps({"type": self.msg_type.value, "message": self.message})

    # Convert JSON string back to Message object
    @staticmethod
    def from_json(json_str: str) -> "Message":
		# Catch any value errors
        try:
            data = json.loads(json_str)
            return Message(msg_type=MsgType(data["type"]), message=data["message"])
        except ValueError as e:
            print(f"Invalid json: {e}")

@dataclass
class RegenInstructRequestObj:
	step_number: str
	false_action: str

	# Convert JSON string back to RegenInstructRequest object
	@staticmethod
	def from_json(json_str: str) -> "RegenInstructRequestObj":
		# Catch any value errors
		try:
			data = json.loads(json_str)
			return RegenInstructRequestObj(step_number=data["step_number"], false_action=data["false_action"])
		except ValueError as e:
			print(f"Invalid json: {e}")

@dataclass
class TrimLengths:
	from_start: float
	from_end: float

	# Convert TrimLengths object to JSON string
	def to_json(self) -> str:
	    return json.dumps({"from_start": str(self.from_start), "from_end": str(self.from_end)})

			
# Callbacks -------------------------------------------------------------------
# Called for every client connecting (after handshake)
def new_client(client, server):
	print("New client connected and was given id %d" % client['id'])
	
	# Flag as connected
	connected.set()
	connect_state_changed.set()
	
	# Communicate sample interval
	send_msg(MsgType.SAMPLERATE, str(sample_interval))

# Called for every client disconnecting
def client_left(client, server):
	print("Client disconnected")
	
	# Flag as disconnected
	connected.clear()
	connect_state_changed.set()
	
	hl2ss_connected.clear()

# Called when a client sends a message
def message_received(client, server, message):
	# print("Client(%d) said: %s" % (client['id'], message))
	global current_wf_name
	global current_step

	# Deserialize message
	decoded_msg = Message.from_json(message)

	# Unpack message depending on message type
	msg_type = decoded_msg.msg_type
	msg_content = decoded_msg.message
	
	if msg_type == MsgType.DEBUG:
		# Print debug message
		print(msg_content)
	elif msg_type == MsgType.IPADDRESS:
		# Received ip address of hl2
		print(f"HL2 ip address is: {msg_content}")
		
		# Connect hl2ss on separate thread
		stream_thread = Thread(target=connect_stream, args=[msg_content])
		stream_thread.daemon = True
		stream_thread.start()
	elif msg_type == MsgType.MODE:
		# Received mode chosen by user
		print(f"User chose {msg_content} mode")
		
		# Display mode (handle in gui)
		if msg_content == "assisted":
			assisted_auth_mode.set()
			manual_auth_mode.clear()
			guidance_mode.clear()

			# Signal to streaming module
			assisted_mode.set()
		elif msg_content == "manual":
			assisted_auth_mode.clear()
			manual_auth_mode.set()
			guidance_mode.clear()

			# Signal to streaming module
			assisted_mode.clear()
		elif msg_content == "guidance":
			assisted_auth_mode.clear()
			manual_auth_mode.clear()
			guidance_mode.set()
		
		mode_changed.set()
	elif msg_type == MsgType.CAPTURESTART:
		print(f"User started capturing on HL2")
		
		# Clear any previously prepared frames (in case of recapture) adn reset frame counter
		prepared_frames.clear()

		# Flag as capturing
		user_is_capturing.set()

		# Log
		logger.log("video_capture_started")
	elif msg_type == MsgType.CAPTUREEND:
		print("User stopped capturing on HL2")
		
		# Flag as not capturing anymore
		user_is_capturing.clear()

		# Visualize prepared frames in gui (only in assisted mode)
		if assisted_auth_mode.is_set():
			# Reduce frames to relevant ones (including hand + buffer)
			extract_frames_with_hands()
			grid_from_prepared_frames()

		# Log
		logger.log("video_capture_finished")
	elif msg_type == MsgType.TEXTINSTRUCT:
		print(f"Received textual instruction: {msg_content}")

		# Save textual instruction in corresponding folder
		with open(f"./Workflows/{current_wf_name}/step_{current_step}/text_instruction.txt", "w") as text_file:
			text_file.write(f"{msg_content}")

		# Store textual instruction for processing by VLM
		with vlm.text_lock:
			vlm.textual_instruction = msg_content
			
		# Display text instruction in gui
		new_text_instruction_available.set()
	elif msg_type == MsgType.VIDEOCONFIRMED:
		print("User finalized captured video")

		# Start processing frames
		process_frames.set()

		# Log
		logger.log("confirmed_video")
	elif msg_type == MsgType.HANDUSED:
		print(f"Server disagrees regarding used hand. Should be {msg_content}")

		# Cache value
		global hand_used_server
		hand_used_server = msg_content

		# Update gui about correct used hand
		received_used_hand.set()
	elif msg_type == MsgType.ROTDIRECTION:
		print(f"Server disagrees regarding rotation direction. Should be {msg_content}")

		# Cache value
		global rot_direction_server
		rot_direction_server = msg_content

		# Update gui about rotation direction
		received_rot_direction.set()
	elif msg_type == MsgType.WFNAME:
		print(f"User created new workflow with the name '{msg_content}'")
		
		# Save workflow name
		current_wf_name = msg_content

		# Create general workflow folder (if none created yet)
		gen_wf_path = "./Workflows"
		if not os.path.exists(gen_wf_path):
			os.makedirs(gen_wf_path)
			
		# Create folder for this particular workflow
		wf_path = f"{gen_wf_path}/{current_wf_name}"
		if not os.path.exists(wf_path):
			os.makedirs(wf_path)

		# Set path for logger to this workflow folder
		log_path = f"{wf_path}/logs.jsonl"
		logger.set_log_path(log_path)

		# Create folder for initial step
		step_path = f"{wf_path}/step_{current_step}"
		if not os.path.exists(step_path):
			os.makedirs(step_path)
	elif msg_type == MsgType.NEWSTEP:
		print(f"User started authoring new step {msg_content}")
		
		# Save current step number
		current_step = msg_content

		# Create folder for new step
		step_path = f"./Workflows/{current_wf_name}/step_{current_step}"
		if not os.path.exists(step_path):
			os.makedirs(step_path)

		# Inform gui about new step
		moved_to_next_step.set()

		# # Log
		logger.log("proceeded_to_next_step")
	elif msg_type == MsgType.AUTHFINISH:
		print("User finished authoring process")
		
		# Log
		logger.log("authoring_finished")
		logger.close()

		# Terminate hl2ss connection
		hl2ss_connected.clear()

		# Terminate websocket connection (ws server keeps running though)
		ws_server.disconnect_clients_gracefully()
	elif msg_type == MsgType.GUIDEFINISH:
		print("User terminated workflow guidance")
		
		# Terminate hl2ss connection
		hl2ss_connected.clear()

		# Terminate websocket connection (ws server keeps running though)
		ws_server.disconnect_clients_gracefully()
	elif msg_type == MsgType.REGENERATE:
		print(f"User requested regeneration of instructions")

		# Deserialize json message
		regen_info = RegenInstructRequestObj.from_json(msg_content)
		print(f"{regen_info}")

		reanalyze_frames(False, False, regen_info)
	elif msg_type == MsgType.AUTHSTART:
		print(f"User started authoring process")

		# Log
		logger.log("authoring_started")
	elif msg_type == MsgType.NEWSTEPINSITU:
		print(f"User proceeded to next step in in-situ review authoring")

		# Log
		if msg_content == "1":
			logger.log("started_insitu")
		else:
			logger.log("proceeded_to_next_step_insitu")
	elif msg_type == MsgType.AUTHMODAL:
		print(f"User selected authoring modality in manual mode")

		# Log
		if msg_content == "text":
			logger.log("started_auth_text")
		elif msg_content == "video":
			logger.log("started_auth_video")
		elif msg_content == "3d":
			logger.log("started_auth_3d")
	elif msg_type == MsgType.AUTHMODALFINISH:
		print(f"User finished authoring modality in manual mode")

		# Log
		logger.log("finished_auth_modality")
	elif msg_type == MsgType.DIDMANIPULATE:
		print(f"User manipulated 3D instruction in assisted in-situ authoring")

		# Log
		logger.log("manipulated_3d")
	elif msg_type == MsgType.DIDEDITTEXT:
		print(f"User edited text in assisted in-situ authoring")

		# Log
		logger.log("edited_text")
	elif msg_type == MsgType.DIDARRANGE:
		print(f"User arranged panel")

		# Log
		logger.log("arranged_panel")
	else:
		print("Could not determine message type")
	
# Methods ---------------------------------------------------------------------
# Create websocket server
def create_server():
	# Websocket configs
	global ws_server, ws_host, ws_port
	ws_server = WebsocketServer(host = ws_host, port = ws_port)
	print(f"Set up websocket server on: ws://{ws_host}:{ws_port}")

	# Connect callbacks
	ws_server.set_fn_new_client(new_client)
	ws_server.set_fn_client_left(client_left)
	ws_server.set_fn_message_received(message_received)

	# Start websocket server
	ws_server.run_forever()
	
def send_msg(msg_type, msg):
	# Encode json message
	message = Message(msg_type, msg)
	json_msg = message.to_json()
	
	# Send message
	global ws_server
	ws_server.send_message_to_all(json_msg)