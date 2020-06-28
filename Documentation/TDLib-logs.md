When you want to report an issue with the Unigram app, you could be asked to send your logs. Notice: never send a log to an unknown person, send it only to the Unigram developer or the [@tdlib_bot](https://t.me/tdlib_bot).

### Opening the Diagnostic settings without signing in

While you are in the sign-in screen, click the app version 10 times.

### Opening the Diagnostic settings while being signed in

Open the hamburger menu, select Settings, then click on the app version 10 times

### Enabling the logging

While being in the Diagnostic settings, under `TDLib Logs` select `Verbosity level` and set it to `Debug`.

### Sending a log file to a user
To send logs through the app you must disable them first:
Navigate to the Diagnostics settings, select `Verbosity level` and set it to `Assert`.
You can now click on the item below (marked as `tdlib_log.txt`): a dialog will open and you will be able to pick a chat where to send the file.

### Finding the log file manually

While you are signed out, you won't be able to send logs via the app, you have to browse for the log manually. Navigate to `%LOCALAPPDATA%\Packages\38833FF26BA1D.UnigramPreview_g9c9v27vpyspw\LocalState` location (the easiest way is to press `Windows+R` and paste the path there). Grab the `tdlib_log.txt` file and send it.
