SKYBRIDGE 64
N64 controls for Wild Blue Skies
by papertowerstudios  -  free to use and share


WHAT IT DOES

Wild Blue Skies is played with an Xbox-style controller. Skybridge 64 lets you
play it with a real Nintendo 64 controller instead, mapped the way Star Fox 64
(Lylat Wars in PAL regions) played on the original hardware:

    Stick      Steer your ship
    A          Laser  -  hold to charge
    B          Smart Bomb
    Z / L      Tilt left   -  double-tap to roll
    R          Tilt right  -  double-tap to roll
    C left     Boost
    C down     Brake
    C right    Radio
    START      Pause

ONE BUTTON FOR BOTH SHOTS

In Star Fox 64 there is no separate button for the tracking / lock-on shot.
A does both, and how long you hold it decides which one you get:

    tap A       normal laser shot
    hold A      the laser charges  -  this is the tracking shot
    release A   the charged shot fires

Holding past 370 ms switches from firing to charging. Tapping is not throttled:
a press fires at once, and holding adds two follow-up shots before the laser
goes quiet - the same burst pattern as the original, up to about 15 shots per
second.

A also confirms menus and skips cutscenes.


HOW TO USE

0. Unpack the whole ZIP into one folder and keep the files together.
1. Start Skybridge64.exe.
2. Connect your N64 controller  -  a USB adapter or a wireless pad.
   Around 40 adapter models are recognised automatically. An unknown one asks
   you to press each button once and is remembered from then on.
3. Start the game.

That order matters. The game picks its controllers at startup.

Other controllers may stay plugged in. The status line shows which player
number Windows gave the N64 pad; the game accepts it either way.

Nothing to install and nothing to configure. The first time you run it,
Skybridge 64 may ask to set up a small driver: press "Set up now", confirm the
Windows prompt, and it carries on by itself. The driver installer is the file
ViGEmBus_1.22.0_x64_x86_arm64.exe sitting next to the program - nothing is
downloaded, and you may run or inspect it yourself first. It is signed by
Nefarius Software Solutions.

Windows may warn about an unknown publisher because this program is not code
signed. Choose "More info" and then "Run anyway".


IF SOMETHING DOES NOT WORK

The game does not react at all
    Start Skybridge 64 first, then the game. If the pad was connected after
    the game started, press "Reconnect pad" and restart the game.

The pad fires by itself, all buttons at once
    A Nintendo wireless N64 controller connected by USB cable does this. The
    cable is for charging only  -  connect it over Bluetooth to play.

Nothing happens at all
    Check the status line at the top. It says what is missing.

"The ViGEmBus installer is missing"
    The files from the ZIP were separated. Keep Skybridge64.exe, the DLL and
    the ViGEmBus installer together in the same folder.

F8 pauses and resumes at any time, even while the game is in focus.


SOURCE CODE

Skybridge 64 is open source under the MIT licence:
https://github.com/papertowerstudios/skybridge64


INCLUDED SOFTWARE

This program contains ViGEmBus and ViGEm.NET by Nefarius Software Solutions
e.U., used under the BSD 3-Clause and MIT licences. The full licence texts are
in ViGEmBus-LICENSE.txt and ViGEm.NET-LICENSE.txt. Both are independent
projects; their authors have no involvement in and give no endorsement to
Skybridge 64.


Skybridge 64 is an unofficial, fan-made tool. It is not affiliated with,
authorised by or endorsed by Nintendo or by the makers of Wild Blue Skies.
"Nintendo 64", "Star Fox 64", "Lylat Wars" and "Wild Blue Skies" are named
only to say which hardware this tool works with, which game it targets, and
which control scheme it reproduces. All trademarks belong to their respective
owners.
