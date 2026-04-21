# Imports -------------------------------------------------------------------------
import tkinter as tk
import tkinter.ttk as ttk
from threading import Thread
import socket
import sys

# Custom modules
import language
from language import german, english
from helper import create_img, opencv_in_tkinter
# from wss import create_wss, wss_state_changed, ws_connected
import wss
from streaming import hl2ss_state_changed, hl2ss_connected, show_pv
# from video_analyzer import new_grid_available, grid_lock, grid_img
import video_analyzer as va
import vlm

# Vars ----------------------------------------------------------------------------
counting_down = False
event_check_interval = 333 # in milliseconds

# Gui creator ---------------------------------------------------------------------
def create_gui():
    # UI Callbacks ----------------------------------------------------------------

    # Callback for switching language
    def on_language_clicked():
        # Check current language
        if language.current_language == 0:
            # Change language to English
            language.current_language = 1
        
            # Change text elements
            main_window.title(english["window"])
            instruction_title.config(text=english["iHeader"])
            instruction_txt_1.config(text=english["i1"])
            instruction_txt_2.config(text=english["i2"])
            language_label.config(text=english["language"])
            exit_label.config(text=english["exit"])
            status_title.config(text=english["statusHeader"])
            status_info.config(text=(english["statusInfo"][1] if wss.connected.is_set() else english["statusInfo"][0]))
            auth_mode_label.config(text=(english["modeHeader"]))
            info_title.config(text=english["infoHeader"])
            auth_title.config(text=english["authHeader"])
            pv_vis_option.config(text=english["visPv"])
            text_instruction_label.config(text=english["textHeader"])
            grid_title.config(text=english["gridHeader"])
            analysis_title.config(text=english["analysisHeader"])
            action_label.config(text=english["actionLabel"])
            aframe_label.config(text=english["actionFrameLabel"])
            hand_used_label.config(text=english["handsLabel"])       
            rot_label.config(text=english["rotDirectionLabel"])
            gen_text_label.config(text=english["genTextLabel"])
            response_time_label.config(text=english["responseTimeLabel"])
    
            # Change image
            language_button.config(image=en_img)
        else:
            # Change language to German
            language.current_language = 0
        
            # Change text elements
            main_window.title(german["window"])
            instruction_title.config(text=german["iHeader"])
            instruction_txt_1.config(text=german["i1"])
            instruction_txt_2.config(text=german["i2"])
            language_label.config(text=german["language"])
            exit_label.config(text=german["exit"])
            status_title.config(text=german["statusHeader"])
            status_info.config(text=german["statusInfo"][1] if wss.connected.is_set() else german["statusInfo"][0])
            auth_mode_label.config(text=(german["modeHeader"]))
            info_title.config(text=german["infoHeader"])
            auth_title.config(text=german["authHeader"])
            pv_vis_option.config(text=german["visPv"])
            text_instruction_label.config(text=german["textHeader"])
            grid_title.config(text=german["gridHeader"])
            analysis_title.config(text=german["analysisHeader"])
            action_label.config(text=german["actionLabel"])
            aframe_label.config(text=german["actionFrameLabel"])
            hand_used_label.config(text=german["handsLabel"])
            rot_label.config(text=german["rotDirectionLabel"])
            gen_text_label.config(text=german["genTextLabel"])
            response_time_label.config(text=german["responseTimeLabel"])
    
            # Change image
            language_button.config(image=de_img)

    # Callback for exit
    def on_exit_clicked():
        # Callbacks for user selection
        def on_yes_click():
            # Remove dialog window
            confirm_dialog.destroy()
        
            # Quit application
            # quit()
            sys.exit()

        def on_no_click():
            # Do nothing but remove window
            confirm_dialog.destroy()
        
        # Create pop up window (text, yes, no options)
        confirm_dialog = tk.Toplevel(main_window)
        confirm_dialog.title((english["confirmHeader"][0], german["confirmHeader"][0])[language.current_language == 0])

        # Button styles
        button_style = ttk.Style()
        button_style.configure("no.TButton", font=("Helvetica", 14, "bold"), foreground="dodgerblue1")
        button_style.configure("yes.TButton", font=("Helvetica", 14), foreground="crimson")

        confirm_txt = ttk.Label(confirm_dialog, text=(english["confirmTxt"][0], german["confirmTxt"][0])[language.current_language == 0])
        confirm_txt.pack(padx=10, pady=10) 
    
        yes_button = ttk.Button(confirm_dialog, text=(english["confirmYes"], german["confirmYes"])[language.current_language == 0], command=on_yes_click)
        yes_button.config(style="yes.TButton")
        yes_button.pack(side="right", padx=5, pady=5)

        no_button = ttk.Button(confirm_dialog, text=(english["confirmNo"], german["confirmNo"])[language.current_language == 0], command=on_no_click)
        no_button.config(style="no.TButton")
        no_button.pack(side="left", padx=5, pady=5)

        # Window configurations
        # Position window in screen center
        main_window.eval(f'tk::PlaceWindow {str(confirm_dialog)} center')
        # Force interaction
        confirm_dialog.grab_set()
        # Disable minimization, maximization of window
        confirm_dialog.resizable(0, 0)
        
    # Callback for pv visualization
    def pv_vis_toggled():        
        # Set pv visualization state
        # global show_pv
        if pv_vis_enabled.get():
            # Enable
            show_pv.set()
            
            print("Enabled pv visualization")
        else:
            # Disable
            show_pv.clear()
            
            print("Disabled pv visualization")

    # Gui Layout ------------------------------------------------------------------
    # Root window
    main_window = tk.Tk()    
    main_window.title(german["window"])

    # Configure columns and rows to expand with window resizing
    main_window.columnconfigure(0, weight=1)
    main_window.columnconfigure(1, weight=1)
    main_window.rowconfigure(0, weight=1)
    main_window.rowconfigure(1, weight=1)
    main_window.rowconfigure(2, weight=1)

    # Set minimum size of the main window to avoid overlapping frames
    main_window.update_idletasks()
    main_window.minsize(main_window.winfo_width(), main_window.winfo_height())
    main_window.attributes("-fullscreen", True)

    # Pre Configurations ----------------------------------------------------------
    # Default styles (Must be defined after window is created)
    default_style = ttk.Style()
    default_style.configure("TButton", font=("Helvetica", 18, "bold"), foreground="dodgerblue1")
    # defaultStyle.configure("TEntry", font=("Helvetica", 16), foreground="black")       # Not working
    default_style.configure("TLabel", font=("Helvetica", 18), foreground="black")
    default_style.configure("header.TLabel", font=("Helvetica", 25, "bold"), foreground="dodgerblue2")
    default_style.configure("TCheckbutton", font=("Helvetica", 15))
    default_style.configure("TMenubutton", font=("Helvetica", 12, "bold"))

    # Set current language: German (0), English (1)
    language.current_language = 0

    # Load images
    flag_icon_height = 30
    exit_icon_height = 30
    status_icon_height = 30
    grid_img_height = 300

    en_img = create_img("./images/usaFlag.png", flag_icon_height)
    de_img = create_img("./images/germanFlag.png", flag_icon_height)
    exit_img = create_img("./images/exit.png", exit_icon_height)
    connected_img = create_img("./images/connected.png", status_icon_height)
    not_connected_img = create_img("./images/notConnected.png", status_icon_height)
    grid_placeholder_img = create_img("./images/grid.png", grid_img_height)

    # Top (header) frame ----------------------------------------------------------
    top_frame = tk.Frame(main_window)
    # top_frame.config(highlightbackground="black", highlightthickness=1)
    top_frame.grid(row=0, column=0, columnspan=2, padx=10, pady=10, sticky="ne")
    top_frame.columnconfigure(0, weight=1)
    top_frame.columnconfigure(1, weight=1)

    # Exit application
    exit_label = ttk.Label(top_frame, text=german["exit"])
    exit_label.grid(row=0, column=0, padx=5, pady=5)

    exit_button = ttk.Button(top_frame, image=exit_img)
    exit_button.config(command=on_exit_clicked)
    exit_button.grid(row=0, column=1, padx=5, pady=5)

    # Center frame (contains left and right frame) --------------------------------
    center_frame = tk.Frame(main_window)
    center_frame.config(highlightbackground="black", highlightthickness=3)
    center_frame.grid(row=1, column=0, padx=10, pady=10)
    center_frame.columnconfigure(0, weight=1)

    # Left frame (contains instruction and connection status frame) ----------------------
    left_frame = tk.Frame(center_frame)
    # left_frame.config(highlightbackground="black", highlightthickness=1)
    left_frame.grid(row=1, column=0, padx=10, pady=10, sticky="wns")
    left_frame.columnconfigure(0, weight=1)

    # Instruction frame -----------------------------------------------------------
    instruction_frame = tk.Frame(left_frame)
    # instruction_frame.config(highlightbackground="black", highlightthickness=1)
    instruction_frame.grid(row=0, column=0, padx=10, pady=10, sticky="we")
    instruction_frame.columnconfigure(0, weight=1)

    # Instructions title
    instruction_title = ttk.Label(instruction_frame, text=german["iHeader"], style="header.TLabel")
    instruction_title.grid(row=0, column=0, padx=5, pady=20, sticky="nsw")

    # Instructions text
    t1 = german["i1"]
    t2 = german["i2"]
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.connect(("8.8.8.8", 80))
    # t3 = socket.gethostbyname(socket.gethostname()) + ":9001"
    t3 = s.getsockname()[0] + f":{wss.ws_port}"
    s.close()

    instruction_txt_1 = ttk.Label(instruction_frame, text=t1)
    instruction_txt_1.grid(row=1, column=0, padx=5, pady=15, sticky="w")
    instruction_txt_2 = ttk.Label(instruction_frame, text=t2)
    instruction_txt_2.grid(row=2, column=0, padx=5, pady=15, sticky="w")
    instruction_txt_3 = ttk.Label(instruction_frame, text=t3, font=("Helvetica", 18, "bold"))
    instruction_txt_3.grid(row=3, column=0, padx=5, pady=15, sticky="w") 

    # Status frame ----------------------------------------------------------------
    status_frame = tk.Frame(left_frame)
    # status_frame.config(highlightbackground="black", highlightthickness=1)
    status_frame.grid(row=2, column=0, padx=5, pady=10, sticky="nswe")
    status_frame.columnconfigure(0, weight=1)

    # Status title
    status_title = ttk.Label(status_frame, text=german["statusHeader"], style="header.TLabel")
    status_title.grid(row=0, column=0, padx=5, pady=20, sticky="nsw")

    # Status info text
    status_info = ttk.Label(status_frame, text=german["statusInfo"][0]) # 0: Not connected by default
    status_info.grid(row=0, column=1, padx=5, pady=20, sticky="nsw")

    # Status indicator
    status_indicator = ttk.Label(status_frame, image=not_connected_img)
    status_indicator.grid(row=0, column=2, padx=10, pady=20, sticky="nsw")
    
    # Visualization option
    pv_vis_enabled = tk.BooleanVar(value=False)
    pv_vis_option = ttk.Checkbutton(status_frame, text=german["visPv"], variable=pv_vis_enabled, command=pv_vis_toggled)
    pv_vis_option.grid(row=1, column=0, padx=10, pady=5, sticky="wns")
    pv_vis_option.config(state="disable")
    
    # Auth mode frame ------------------------------------------------------------
    # mode_frame = tk.Frame(left_frame)
    # mode_frame.grid(row=4, column=0, padx=5, pady=10, sticky="nswe")
    # mode_frame.columnconfigure(0, weight=1)

    # # Auth mode title
    # mode_title = ttk.Label(mode_frame, text=german["modeHeader"], style="header.TLabel")
    # mode_title.grid(row=0, column=0, padx=5, pady=20, sticky="nsw")

    # # Auth mode label
    # mode_label = ttk.Label(mode_frame, text="/")
    # mode_label.grid(row=0, column=1, padx=5, pady=20, sticky="nsw")

    # Information frame -----------------------------------------------------------
    info_frame = tk.Frame(left_frame)
    # info_frame.config(highlightbackground="black", highlightthickness=1)
    info_frame.grid(row=6, column=0, padx=10, pady=10, sticky="nwe")
    info_frame.columnconfigure(0, weight=1)

    # Information title
    info_title = ttk.Label(info_frame, text=german["infoHeader"], style="header.TLabel")
    info_title.grid(row=0, column=0, columnspan=2, padx=5, pady=20, sticky="wns")

    # Information scrollable text (text widget + scrollbar)
    text_widget = tk.Text(info_frame, wrap="word")
    # text_widget.insert(tk.END, "This is a debug message")
    text_widget.config(font=("Helvetica", 13), state="disabled")
    text_widget.grid(row=1, column=0, padx=5, sticky="nsew")

    vsb = ttk.Scrollbar(info_frame, command=text_widget.yview, orient="vertical")
    text_widget.config(yscrollcommand=vsb.set, height=8)
    vsb.grid(row=1, column=1, padx=5, sticky="ns")
    
    # Redirects all prints to this dedicated text widget
    def decorator(func):
        def inner(input_str):
            # Ignore newlines
            if input_str == "\n":
                return inner
            
            try:
                # Need to enable text widget first
                text_widget.config(state="normal")    

                # Remove any existing new lines at the end
                input_str += "\n"
                text_widget.insert("1.0", input_str)

                # Disable widget again to prevent editing of messages
                text_widget.config(state="disabled")
                # print_to_text_widget(input_str, text_widget)
                
                return func(input_str)
            except:
                return func(input_str)
        return inner
    sys.stdout.write=decorator(sys.stdout.write)

    # Right frame (contains information frame and pv visualization frame) ---------
    right_frame = tk.Frame(center_frame)
    # right_frame.config(highlightbackground="black", highlightthickness=1)
    right_frame.grid(row=1, column=2, padx=10, pady=10, sticky="ens")
    right_frame.columnconfigure(0, weight=1)

    # Authoring frame ------------------------------------------------------
    auth_frame = tk.Frame(right_frame)
    # vis_options_frame.config(highlightbackground="black", highlightthickness=1)
    auth_frame.grid(row=0, column=0, padx=10, pady=10, sticky="nws") 
    auth_frame.columnconfigure(0, weight=1)

    # Auth title
    auth_title = ttk.Label(auth_frame, text=german["authHeader"], style="header.TLabel")
    auth_title.grid(row=0, column=0, padx=5, pady=2, sticky="wns")
    
    # Authoring mode
    auth_mode_label = ttk.Label(auth_frame, text=german["modeHeader"], font=("Helvetica", 12, "bold"))
    auth_mode_label.grid(row=1, column=0, padx=5, pady=2, sticky="nsw")
    auth_mode_value = ttk.Label(auth_frame, text="/", font=("Helvetica", 12))
    auth_mode_value.grid(row=2, column=0, padx=5, pady=2, sticky="nsw")    

    # Textual instruction
    text_instruction_label = ttk.Label(auth_frame, text=german["textInstruction"], font=("Helvetica", 12, "bold"))
    text_instruction_label.grid(row=3, column = 0, padx=5, pady=2, sticky="nsw")   
    text_instruction = ttk.Label(auth_frame, text="/", font=("Helvetica", 12))
    text_instruction.grid(row=4, column=0, padx=5, pady=2, sticky="nsw")
    
    # Video instruction (grid)
    grid_title = ttk.Label(auth_frame, text=german["gridHeader"], font=("Helvetica", 12, "bold"))
    grid_title.grid(row=5, column=0, padx=5, pady=2, sticky="nsw")
    
    grid_img_label = ttk.Label(auth_frame, text="/", font=("Helvetica", 12))
    grid_img_label.grid(row=6, column=0, padx=10, pady=2, sticky="nsew")

    # Analysis frame --------------------------------------------------------------
    analysis_frame = tk.Frame(right_frame)
    analysis_frame.grid(row=2, column=0, padx=10, pady=10, sticky="nws") 
    analysis_frame.columnconfigure(0, weight=1)

    # Analysis title
    analysis_title = ttk.Label(analysis_frame, text=german["analysisHeader"], style="header.TLabel")
    analysis_title.grid(row=0, column=0, padx=5, pady=10, sticky="nsw")

    # ChatGPT and Gemini headers
    # gpt_header = ttk.Label(analysis_frame, text="ChatGPT", font=("Helvetica", 12, "bold"))
    # gpt_header.grid(row=3, column=0, padx=5, pady=10, sticky="nswe")
    # gemini_header = ttk.Label(analysis_frame, text="Gemini", font=("Helvetica", 12, "bold"))
    # gemini_header.grid(row=4, column=0, padx=5, pady=10, sticky="nswe")

    # Action, action frame, and explanation label
    action_label = ttk.Label(analysis_frame, text=german["actionLabel"], font=("Helvetica", 12, "bold"))
    action_label.grid(row=1, column=0, padx=5, pady=10, sticky="nsw")
    aframe_label = ttk.Label(analysis_frame, text=german["actionFrameLabel"], font=("Helvetica", 12, "bold"))
    aframe_label.grid(row=2, column=0, padx=5, pady=10, sticky="nsw")  
    hand_used_label = ttk.Label(analysis_frame, text=german["handsLabel"], font=("Helvetica", 12, "bold"))
    hand_used_label.grid(row=3, column=0, padx=5, pady=10, sticky="nsw")
    rot_label = ttk.Label(analysis_frame, text=german["rotDirectionLabel"], font=("Helvetica", 12, "bold"))
    rot_label.grid(row=4, column=0, padx=5, pady=10, sticky="nsw")
    gen_text_label = ttk.Label(analysis_frame, text=german["genTextLabel"], font=("Helvetica", 12, "bold"))
    gen_text_label.grid(row=5, column=0, padx=5, pady=10, sticky="nsw")
    response_time_label = ttk.Label(analysis_frame, text=german["responseTimeLabel"], font=("Helvetica", 12, "bold"))
    response_time_label.grid(row=6, column=0, padx=5, pady=10, sticky="nsw")
    
    # ChatGPT result values
    gpt_action_value = ttk.Label(analysis_frame, text="/", font=("Helvetica", 12))
    gpt_action_value.grid(row=1, column=1, padx=5, pady=10, sticky="nswe")
    gpt_frames_value = ttk.Label(analysis_frame, text="/", font=("Helvetica", 12))
    gpt_frames_value.grid(row=2, column=1, padx=5, pady=10, sticky="nswe")    
    gpt_hand_used_value = ttk.Label(analysis_frame, text="/", font=("Helvetica", 12))
    gpt_hand_used_value.grid(row=3, column=1, padx=5, pady=10, sticky="nswe")
    gpt_rot_value = ttk.Label(analysis_frame, text="/", font=("Helvetica", 12))
    gpt_rot_value.grid(row=4, column=1, padx=5, pady=10, sticky="nswe")
    gpt_gen_text_value = ttk.Label(analysis_frame, text="/", font=("Helvetica", 12))
    gpt_gen_text_value.grid(row=5, column=1, padx=5, pady=10, sticky="nswe")
    gpt_response_time_value = ttk.Label(analysis_frame, text="/", font=("Helvetica", 12))
    gpt_response_time_value.grid(row=6, column=1, padx=5, pady=10, sticky="nswe")
    
    # Gemini result values
    # gemini_action_value = ttk.Label(analysis_frame, text="/", font=("Helvetica", 12))
    # gemini_action_value.grid(row=4, column=2, padx=5, pady=10, sticky="nswe")
    # gemini_frame_value = ttk.Label(analysis_frame, text="/", font=("Helvetica", 12))
    # gemini_frame_value.grid(row=4, column=3, padx=5, pady=10, sticky="nswe")
    # gemini_explanation_value = ttk.Label(analysis_frame, wraplength=750, font=("Helvetica", 12), text="/")
    # gemini_explanation_value.grid(row=4, column=4, padx=5, pady=10, sticky="nswe")

    # Bottom (footer) frame -------------------------------------------------------
    bottom_frame = tk.Frame(main_window)
    # bottom_frame.config(highlightbackground="black", highlightthickness=1)
    bottom_frame.grid(row=3, column=0, columnspan=2, padx=10, pady=10, sticky="es")
    bottom_frame.columnconfigure(0, weight=1)

    # Language options
    language_label = ttk.Label(bottom_frame, text=german["language"])
    language_label.grid(row=0, column=0, padx=5, pady=5, sticky="e")

    language_button = ttk.Button(bottom_frame, image=de_img) # German by default
    language_button.config(command=on_language_clicked)
    language_button.grid(row=0, column=1, padx=5, pady=5, sticky="es")
    
    # Separators ------------------------------------------------------------------
    # Left and right
    lr_separator = ttk.Separator(center_frame, orient="vertical")
    lr_separator.grid(row=1, column=1, sticky="ns")
    
    # Left: Instructions and status
    l_is_separator = ttk.Separator(left_frame, orient="horizontal")
    l_is_separator.grid(row=1, column=0, sticky="we")
  
    # Left: Status and information
    l_sa_separator = ttk.Separator(left_frame, orient="horizontal")
    l_sa_separator.grid(row=3, column=0, sticky="we")

    # Right: Auth mode and analysis
    r_ga_separator = ttk.Separator(right_frame, orient="horizontal")
    r_ga_separator.grid(row=1, column=0, sticky="we")
    
    # Right: Analysis table
    # tv_separator = ttk.Separator(analysis_frame, orient="vertical")
    # tv_separator.grid(row=1, column=1, rowspan=4, sticky="ns")
    # th_separator = ttk.Separator(analysis_frame, orient="horizontal")
    # th_separator.grid(row=2, column=0, columnspan=5, sticky="we")


    # Other program code ----------------------------------------------------------
    # Start websocket server on a separate thread
    wss_thread = Thread(target=wss.create_server)
    wss_thread.daemon = True
    wss_thread.start()
    print(f"Started websocket server. Listening for any incoming connections on {wss.ws_host}:{wss.ws_port}")

    # Check cross-thread events every x ms to do stuff on main thread if necessary
    def check_events():
        # Check all relevant event flags

        # Websocket connection change
        if wss.connect_state_changed.is_set():
            # Websocket connected?
            if wss.connected.is_set():
                print("Established websocket connection")
                
                # Update gui
                status_info.config(text=(english["statusInfo"][1], german["statusInfo"][1])[language.current_language == 0])
                status_indicator.config(image=connected_img)
            else:
                print("Terminated websocket connection")  
                
                # Update gui
                status_info.config(text=(english["statusInfo"][0], german["statusInfo"][0])[language.current_language == 0])
                status_indicator.config(image=not_connected_img)
                
                # Terminate hl2ss connection as well if still trying to stream
                if hl2ss_connected.is_set():
                    hl2ss_connected.cear()
                
            # Processed
            wss.connect_state_changed.clear()
            
        # Hl2ss connection change
        if hl2ss_state_changed.is_set():
            # HL2ss connected?
            if hl2ss_connected.is_set():
                # Update gui
                pv_vis_option.config(state="enable")
            else:
                print("Terminated hl2ss connection")
                
                # Update gui
                pv_vis_enabled.set(False)
                show_pv.clear()
                pv_vis_option.config(state="disable")
                
                # Clear any existing displayed textual instruction, grid, and analysis content
                auth_mode_value.config(text="/")
                text_instruction.config(text="/")
                grid_img_label.config(image="")
                gpt_action_value.config(text="/")
                gpt_frames_value.config(text="/")
                gpt_hand_used_value.config(text="/")
                gpt_rot_value.config(text="/")
                gpt_gen_text_value.config(text="/")
                gpt_response_time_value.config(text="/")
                
            # Processed
            hl2ss_state_changed.clear()
            
        # Authoring mode has been specified by user
        if wss.mode_changed.is_set():
            if wss.assisted_auth_mode.is_set():
                # Assisted authoring mode (VLM-based)
                auth_mode_value.config(text="Assisted")
            elif wss.manual_auth_mode.is_set():
                # Manual authoring mode
                auth_mode_value.config(text="Manual")
            elif wss.guidance_mode.is_set():
                # Guidance mode
                auth_mode_value.config(text="Guidance")

            # Processed
            wss.mode_changed.clear()

        # Update gui 
        if wss.moved_to_next_step.is_set():
            # Reset previous analysis results
            text_instruction.config(text="/")
            grid_img_label.config(image="")
            gpt_action_value.config(text="/")
            gpt_frames_value.config(text="/")
            gpt_hand_used_value.config(text="/")
            gpt_rot_value.config(text="/")
            gpt_gen_text_value.config(text="/")
            gpt_response_time_value.config(text="/")

            # Processed
            wss.moved_to_next_step.clear()

        # New text instruction arrived
        if wss.new_text_instruction_available.is_set():
            with vlm.text_lock:
                # Get textual instruction safely
                text_instruction.config(text=vlm.textual_instruction)

            # Processed
            wss.new_text_instruction_available.clear()

        # New stitched grid is ready for render
        if va.new_grid_available.is_set():            
            # Safely access new grid img
            with va.grid_img_lock:
                # from video_analyzer import grid_img
                # Convert grid image into tkinter representable format
                tk_img = opencv_in_tkinter(va.gui_grid_img)
                
            # Display new grid image in gui label
            grid_img_label.config(image=tk_img)
            grid_img_label.image = tk_img   # Keep reference so it is not disposed by tkinter
            
            # Flag as processed
            va.new_grid_available.clear()

        # New result from chatgpt available
        if vlm.gpt_result_available.is_set():
            # Display returned action and action_frame values in gui
            with vlm.gpt_lock:
                gpt_action_value.config(text=vlm.gpt_action)
                gpt_frames_value.config(text=vlm.gpt_frames)
                gpt_hand_used_value.config(text=vlm.gpt_hand_used)
                gpt_rot_value.config(text=vlm.gpt_rot_direction)
                gpt_gen_text_value.config(text=vlm.gpt_gen_text)
                gpt_response_time_value.config(text=vlm.gpt_response_time)

            # Processed
            vlm.gpt_result_available.clear()

        # Server disagrees regarding used hand
        if wss.received_used_hand.is_set():
            # Swap hand used label (left->right, right->left)
            gpt_hand_used_value.config(text=wss.hand_used_server)

            # Processed
            wss.received_used_hand.clear()

        # Rotation direction detected by server
        if wss.received_rot_direction.is_set():
            # Update gui about rotation direction
            gpt_rot_value.config(text=wss.rot_direction_server)

            # Processed
            wss.received_rot_direction.clear()

        # New result from gemini available
        # if vlm.gemini_result_available.is_set():
        #     # Display returned action and action_frame values in gui
        #     with vlm.gemini_lock:
        #         gemini_action_value.config(text=vlm.gemini_action)
        #         gemini_frame_value.config(text=vlm.gemini_frame)
        #         gemini_explanation_value.config(text=vlm.gemini_explanation)
        
        # Recall check_events to keep checking
        main_window.after(event_check_interval, check_events)

    # Gui render loop (blocking, must be at the end!) -----------------------------
    main_window.after(event_check_interval, check_events) # Check flags from other threads
    main_window.mainloop()
    