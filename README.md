# Multiplayer Game Project

## Overview
This project demonstrates a multiplayer game connection system built with Unity and Photon PUN networking. Players can create or join game rooms and play together in real-time.

## Features
- Player name customization with automatic saving
- Simple room creation and joining
- Persistent room system
- Support for up to 10 players per room
- Automatic reconnection handling

## Technologies Used
- Unity [Unity 6000.0.28f1]
- Photon PUN [version 2.49]
- TextMeshPro UI

## Installation
1. Clone this repository:
   ```
   git clone https://github.com/Nischal-012/Spoilz-Task
   ```
2. Open the project in Unity [Unity 6000.0.28f1]
3. Ensure Photon PUN and TextMeshPro packages are properly imported
4. The project should be ready to run

## How to Run
1. Open the project in Unity
2. Load the main menu scene (located at `[Assets/Scenes/MainMenu.unity]`)
3. Press Play in the Unity Editor to test locally or build the project for your target platform

## How to Use
1. When you start the game, you'll see the multiplayer connection screen
2. Enter your desired player name (or use the auto-generated guest name)
3. Enter a room name (default is "testRoom")
4. Click "Join" to enter the game
   - If the room exists, you'll join it
   - If not, a new room will be created automatically

5. The game will start and you can play the game with your friend

## Project Structure
- `Assets/Scripts/UI/CreateAndJoin.cs`: Handles player connection, room creation/joining
- `Assets/Scripts/GameLogic/GameBoardController.cs`: Handles game logic and state and Reconnection logic while in game.

## Potential Improvements
- Room listing functionality
- Player avatar customization
- More robust error handling


## Acknowledgments
- Photon PUN documentation