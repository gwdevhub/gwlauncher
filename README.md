
### [Original Creator Releases/Downloads](https://github.com/GregLando113/gwlauncher/releases)
### [My Releases/Downloads](https://github.com/stheno/gwlauncher/releases)

## Overview

![lnch-add-account](http://puu.sh/wO35w/8c1d8889b4.jpg)
![lnch-edit-account](https://puu.sh/wO35O/37cbf03746.jpg)
![lnch-1](https://puu.sh/wO2tI/2c1aeb4365.jpg)

* * *

*   Able to add accounts to a list in order to quickly launch them on demand.
*   Active column shows what accounts are currently loaded in.
*   Client window title renamed at launch to match the character name. (helps with organization)
*   ".dat Patch" allows you to run multiple clients off of one installation. (Side effects occur when using this, see below for details)
*   Open source.

#### New

* * * *
* Alias column to further identify which account is tied to the particular entered character name.
* For those that do not have much hard drive space but want to run multiple accounts...
	This now checks the path for the dat file. If it does not exist it scans other account entry paths.
	If the entry is inactive it will move the dat file to the clicked account path and run the game.
	If it can't find a dat to move. It will just start the game and get a fresh dat from the server.
* * * *

### Installation

* * *

1.  Click download below.
2.  Select the most recent revision executable. (Or source of you want to compile yourself)
3.  Put the executable in its own folder! Dont be that guy who keeps everything in their downloads folder/desktop please.
*   It seems that it is wanting to be in the visual studio build folder OR Guildwars folder now..
4.  Run the executable.

### Usage

* * *

#### Add an Account

Right click the Main Launcher Window to reveal a context menu, then select _Add New_. The Add Account window will then pop up. Fill out all the information it asks for, then hit Add. The new account will now appear in the main form list. Double click to launch.

#### New
* * * *
#### Edit an Account

Right click the Main Launcher Window to reveal a context menu, then select _Edit Selected_. The same window for add account will pop up, but with one exception. The button does indeed show EDIT. This will change any info and save it.
* * * *

#### Remove an Account

Right click the Main Launcher Window to reveal a context menu, then select _Remove Selected_. The selected account on the main list will then be deleted.

#### Edit Account Information (Accounts.json)

Right click the Main Launcher Window to reveal a context menu, then select _Edit Selected_. The selected account on the main list will then be opened with a form where you can modify information. You may also modify Accounts.json directly, then click _Refresh Accounts_. Here is an example of the Accounts.json:

<pre>[
  {
    "alias": "derp1",
    "email": "derp@derperkins.derp",
    "password": "derpyderp",
    "character": "D E R P Y",
    "gwpath": "C:\\Derp\\GW\\Gw.exe",
    "datfix": true,
    "extraargs": "-windowed"
  },
  {
    "alias": "derp2",
    "email": "derp1@derperkins.derp",
    "password": "asf",
    "character": "Derpyless Derp",
    "gwpath": "C:\\Derp\\GW\\Gw.exe",
    "datfix": true,
    "extraargs": "-windowed"
  },
  {
    "alias": "derp3",
    "email": "derp2@derperkins.derp",
    "password": "poop",
    "character": "Werpy Derp",
    "gwpath": "C:\\Derp\\GW3\\Gw.exe",
    "datfix": false,
    "extraargs": "-windowed"
  },
  {
    "alias": "derp4",
    "email": "derp3@derperkins.derp",
    "password": "iamsomature",
    "character": "Derrrrrp Derp",
    "gwpath": "C:\\Derp\\GW2\\Gw.exe",
    "datfix": false,
    "extraargs": "-windowed"
  },
  {
    "alias": "derp5",
    "email": "derp4@derperkins.derp",
    "password": "I<3GWLauncher",
    "character": "So Much Derp",
    "gwpath": "C:\\Derp\\GW1\\Gw.exe",
    "datfix": false,
    "extraargs": "-windowed"
  }
]
</pre>

This format is fairly straight forward, modify the account info of the selected account, then save the file and re-launch GW Launcher.exe.

#### About the .dat patch

The dat patch as stated above will allow you to run as many clients as you want off of one installation, however there are negative side effects of the clients patched. These effects include:

*   Inventory item icons have a chance of never appearing (empty slot), however the quantity number will always appear.
*   Screenshots cannot be taken. (Guild Wars will just create an empty .jpg file in screens)
*   Templates cannot be saved. (Again only an empty file will be produced)
*   Game updates will fail to update, will cause a hang come updates. (Run the Guild Wars client normally to update in order to fix this)
*   If the game crashes, there is no chance of a reconnect to be possible.

There may be more side effects, however these are the effects I have seen. The general problem is that any process involving writing to Gw.dat will fail.

#### GW Plugins

Well, since you read this far, ill tell you another feature not noted in the overview. If you create a folder named "plugins" within the Gw.exe folder, the launcher will load all .dll's inside this folder on launch. This is beneficial for many reasons as you can load plugins and modules at runtime, for example umod's d3d9.dll can be loaded, avoiding the global hook and its side effects. (uMod's d3d9 dll allows for you to close uMod after launch and is significantly less taxing on performance.) I hope that this feature comes in handy to people.
