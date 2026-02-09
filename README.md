# In-Vehicle Data Monitoring Simulation Kit

## Project Overview
This project is an **In-Vehicle Data Monitoring Simulation Kit** designed as a learning and experimental platform for analyzing **CAN bus communication** without primarily relying on a real vehicle. The system allows users to simulate vehicle functions, generate CAN frames, capture raw CAN signals, and analyze them in a visual and systematic manner.

The project integrates software simulation, embedded systems, and signal analysis to provide a comprehensive understanding of in-vehicle communication systems, particularly the CAN protocol.

---

## System Architecture
The system consists of four main components:
1. **Unity Virtual Control Interface (C#)**  
   A graphical user interface used to simulate vehicle functions such as speed control, gear selection, braking, lighting systems, and windshield wipers.

2. **CAN Frame Generator (STM32)**  
   Commands from the Unity interface are sent via **Serial/UART** to an STM32 microcontroller, which generates and transmits **Classic CAN frames** onto the CAN bus through an **MCP2551 CAN transceiver**.

3. **Logic-Level Signal Capture Module (STM32)**  
   Another STM32 module captures logic-level CAN signals with precise timestamps and streams raw signal data back to a PC via UART.

4. **Python GUI Logic Analyzer**  
   A Python-based graphical application decodes the raw bitstream (including **bit stuffing**) and visualizes CAN waveforms and frame fields, enabling detailed protocol-level analysis.

---

## Technologies Used
- **Unity Engine (C#)** – Virtual vehicle control interface and simulation
- **STM32 Microcontrollers** – CAN frame generation and signal capture
- **MCP2551** – CAN bus transceiver
- **Python** – CAN decoding, signal processing, and waveform visualization
- **Serial/UART Communication** – Data exchange between software and hardware

---

## Project Resources
Additional resources related to this project, including:
- Project report
- Presentation slides
- Demonstration videos
- Reference materials and datasets

are available at the following Google Drive link:

🔗 **Project Resources (Reports, Presentations, Videos, and References):**  
https://drive.google.com/drive/folders/1zRRnDYgDS0U7yssWjZr2K1H1N2O3woV_?usp=sharing

---

## Purpose and Applications
This project is intended for educational and experimental use, supporting learning in areas such as:
- In-vehicle network communication (CAN bus)
- Embedded systems and microcontroller-based development
- Signal analysis and protocol decoding
- Automotive and cyber-physical systems research

It can be extended for further studies in automotive electronics, embedded software development, and vehicle network security analysis.
