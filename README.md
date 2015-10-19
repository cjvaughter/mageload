# mageload

An application loader for the [mageboot] (https://github.com/cjvaughter/mageboot) bootloader, designed for use with the [MAGE 2] (https://github.com/Jacob-Dixon/MAGE2/wiki) system and the [MCU Certification] (https://github.com/Jacob-Dixon/MAGE2/wiki/Microcontroller_Cert) at Oklahoma State University.

## Direct Usage
Select a port with the -p flag, and either specify the port name, or leave it blank to select a port from a list.
The port is saved, so there's no need to select one every time.

    mageload.exe -p COM3
    - or -
    mageload.exe -p

Upload your file with the -f flag

    mageload.exe -f yourProgram.hex

## Atmel Studio 7
Install the mageload extension by double-clicking on the .vsix package.
This will add Deploy and Choose Port to the Tools menu.


Select your port with Choose Port, and upload your program with Deploy (or by pressing F8).
