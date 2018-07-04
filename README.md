# RSMoveMecha
This is a RobotStudio Smart Component to update Mechanism from controller axes.


## What you have to do before compiling:
  - Update ABB.Robotics.* References to Good RobotStudio SDK Version path with ***Project*** - ***Add Reference*** - ***Browse***.
  - On Project Properties:
    - **Application**: Choose good .NET Framework version.
    - **Build Events**: *Post Build Events*: Replace with the good LibraryCompiler.exe Path.
    - **Debug**: *Start External Program*: Replace with the good RobotStudio.exe Path `This not work if project on network drive, let it clear.`
  - In *\RSMoveMecha\RSMoveMecha.en.xml*:
    - Replace **xsi:schemaLocation** value with good one.
  - Same for *\RSMoveMecha\RSMoveMecha.xml*.

### If your project path is on network drive:
##### To get RobotStudio load it:
  - In *$(RobotStudioPath)\Bin\RobotStudio.exe.config* file:
    - Add in section *`<configuration><runtime>`*
      - `<loadFromRemoteSources enable="true"/>`

##### To Debug it:
  - Start first RobotStudio to get RobotStudio.exe.config loaded.
  - Then attach its process in VisualStudio ***Debug*** - ***Attach to Process..***

## Usage
![RSMoveMecha](https://raw.githubusercontent.com/DenisFR/RSMoveMecha/master/RSMoveMecha/RSMoveMecha.jpg)
### Properties
  - ***Controller***:\
The Controller to get data. (Simulated or Real if connected)
  - ***CtrlMechanism***:\
The Controller Mechanism to get data.
  - ***CtrlSpecAxis***:\
Specify which only one axis to take value. 0 will take all.
  - ***Mechanism***:\
The Mechanism to update.
  - ***MechSpecAxis***:\
Specify which only one axis to update. 0 will update all.
  - ***NumberOfAxesStatus***:\
Status of Number of Axes updated.
  - ***AllowUpdate***:\
To Allow updating in Simulation.
### Signals
  - ***Update***:\
Set to high (1) to update the Mechanism with Controller axes values.
