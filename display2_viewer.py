import ctypes
import sys
import cv2
import mss
import numpy as np

# Make process DPI aware so coordinates match screen resolution
try:
    ctypes.windll.user32.SetProcessDPIAware()
except Exception:
    pass

# Windows flag that hides this window from AnyDesk, Zoom, OBS, and screen recorders
WDA_EXCLUDEFROMCAPTURE = 0x00000011

class POINT(ctypes.Structure):
    _fields_ = [("x", ctypes.c_long), ("y", ctypes.c_long)]

window_name = "Display 2 Live Viewer (Python)"
cv2.namedWindow(window_name, cv2.WINDOW_NORMAL)
cv2.resizeWindow(window_name, 960, 540)

with mss.mss() as sct:
    if len(sct.monitors) < 3:
        print("Error: Display 2 not found. Press Win+P and select Extend.")
        print("Available monitors detected:", len(sct.monitors) - 1)
        # Fall back to monitor 1 if only one screen is found
        if len(sct.monitors) < 2:
            sys.exit(1)
        monitor = sct.monitors[1]
    else:
        # sct.monitors[1] is Display 1, sct.monitors[2] is Display 2
        monitor = sct.monitors[2]

    stealth_applied = False

    while True:
        # Capture Display 2 frame
        img = np.array(sct.grab(monitor))
        frame = cv2.cvtColor(img, cv2.COLOR_BGRA2BGR)

        # Draw live mouse position when your cursor moves onto Display 2
        pt = POINT()
        ctypes.windll.user32.GetCursorPos(ctypes.byref(pt))
        rel_x = pt.x - monitor["left"]
        rel_y = pt.y - monitor["top"]
        if 0 <= rel_x < monitor["width"] and 0 <= rel_y < monitor["height"]:
            cv2.circle(frame, (rel_x, rel_y), 8, (0, 0, 255), -1)
            cv2.circle(frame, (rel_x, rel_y), 10, (255, 255, 255), 2)

        cv2.imshow(window_name, frame)

        # Hide the viewer window from AnyDesk capture on Display 1
        if not stealth_applied:
            hwnd = ctypes.windll.user32.FindWindowW(None, window_name)
            if hwnd:
                ctypes.windll.user32.SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)
                stealth_applied = True

        # Press 'q' or click the X button to close
        key = cv2.waitKey(16) & 0xFF
        if key == ord('q') or key == 27:  # 27 is Esc
            break
        if cv2.getWindowProperty(window_name, cv2.WND_PROP_VISIBLE) < 1:
            break

cv2.destroyAllWindows()
