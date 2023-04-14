When you want to report an issue with the Unigram app, you could be asked to send your logs. Notice: never send a log to an unknown person, send it only to the Unigram developer or the [@tdlib_bot](https://t.me/tdlib_bot).

### Opening the Diagnostic settings without signing in

While you are in the sign-in screen, click the app version 10 times.

### Opening the Diagnostic settings while being signed in

Open the hamburger menu, select Settings, then click on the app version 10 times

![image](https://user-images.githubusercontent.com/88960956/232140190-6a6e5fc8-30a4-47bd-ac9f-a1b0b04e321b.png)

### Enabling the logging

While being in the Diagnostic settings, under `TDLib Logs` select `Verbosity level` and set it to `Debug`.

![image](https://user-images.githubusercontent.com/88960956/232140765-7206d355-7310-4794-8711-309edca153cd.png)

### Sending a log file to a user
To send logs through the app you must disable them first:
Navigate to the Diagnostics settings, select `Verbosity level` and set it to `Error`.

![image](https://user-images.githubusercontent.com/88960956/232144724-66b405bd-2525-4506-bec8-8d18355f5759.png)

You can now click on the item below (marked as `tdlib_log.txt`):

![image](https://user-images.githubusercontent.com/88960956/232141489-21fa8085-6903-4f25-a3f1-35ed872a9043.png)

A dialog will open and you will be able to pick a chat where to send the file.

![image](https://user-images.githubusercontent.com/88960956/232142457-f18dea09-bad0-42bf-9a52-ff8fd2ba30d6.png)

### Finding the log file manually

While you are signed out, you won't be able to send logs via the app, you have to browse for the log manually. Navigate to `LocalState` (the easiest way is to press `Windows+R` and paste the path there.). Grab the `tdlib_log.txt` file and send it.

![image](https://user-images.githubusercontent.com/88960956/232142888-527dd1a5-7a36-42f3-8c4c-9c4f27f649ce.png)

#### Where's local state?
If you're using Unigram from the Store, this is the location:
`%LOCALAPPDATA%\Packages\38833FF26BA1D.UnigramPreview_g9c9v27vpyspw\LocalState`
Otherwise, the folder that you're looking for is the following:
`%LOCALAPPDATA%\Packages\TelegramFZ-LLC.Unigram_1vfw5zm9jmzqy\LocalState`
