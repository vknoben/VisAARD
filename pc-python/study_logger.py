import json
import os
import datetime
import threading


# A simple JSON event logger that writes events to a file in a line-delimited format. Used during study trials.
class StudyLogger:
    def __init__(self):
        self.file = None
        self.lock = threading.Lock()

    # -----------------------------
    # Set the file path once per trial
    # -----------------------------
    def set_log_path(self, file_path: str):
        if self.file is not None:
            raise RuntimeError("Logger already initialized.")

        os.makedirs(os.path.dirname(file_path), exist_ok=True)
        # line-buffered append mode
        self.file = open(file_path, "a", buffering=1, encoding="utf-8")

    # -----------------------------
    # Log an event
    # -----------------------------
    def log(self, event_name: str):
        if self.file is None:
            raise RuntimeError("Log path not set. Call set_log_path() first.")

        event = {
            "timestamp": datetime.datetime.now().isoformat(timespec="milliseconds"),
            "event": event_name
        }

        with self.lock:
            self.file.write(json.dumps(event) + "\n")
            self.file.flush()
            os.fsync(self.file.fileno())

    # -----------------------------
    # Close the file at the end of the trial
    # -----------------------------
    def close(self):
        if self.file:
            with self.lock:
                self.file.flush()
                os.fsync(self.file.fileno())
                self.file.close()
                self.file = None


# Global instance for easy use across client application
logger = StudyLogger()