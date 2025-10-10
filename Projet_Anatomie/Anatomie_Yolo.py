import cv2
from ultralytics import YOLO
import socket
import json
import time # Pour voir si la boucle tourne

# --- Configuration UDP ---
UDP_IP = "127.0.0.1"
UDP_PORT = 5052
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
# -------------------------

model = YOLO('yolov8n-pose.pt')
cap = cv2.VideoCapture(0)

print("Démarrage du script Python. En attente de détection...")

while cap.isOpened():
    success, frame = cap.read()
    if success:
        results = model(frame, verbose=False)
        
        data_to_send = None # On initialise à None
        
        if results[0].keypoints and len(results[0].keypoints.xy) > 0:
            keypoints = results[0].keypoints.xy[0].tolist() 
            data_to_send = json.dumps(keypoints).encode('utf-8')
            
            # --- DEBUG : AFFICHER CE QUI EST ENVOYÉ ---
            print(f"Envoi de données : {data_to_send}")
            # ----------------------------------------
            
            sock.sendto(data_to_send, (UDP_IP, UDP_PORT))
            
        else:
            # --- DEBUG : SAVOIR QUAND RIEN N'EST DÉTECTÉ ---
            print("Aucune personne détectée dans cette image.")
            # ---------------------------------------------
            
        annotated_frame = results[0].plot()
        cv2.imshow("YOLOv8 Pose Tracking", annotated_frame)

        if cv2.waitKey(1) & 0xFF == ord("q"):
            break
            
        time.sleep(0.1) # Pause plus longue - envoi toutes les 100ms (~10 FPS)
    else:
        break

print("Arrêt du script Python.")
cap.release()
cv2.destroyAllWindows()