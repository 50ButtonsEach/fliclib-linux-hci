# Persistent button metadata example

This is an example how to attach persistent metadata to button events.

## How it works

On start the program fetches a lists of verified Flic buttons and adds a record for each button to a `buttons.json` file. The record can store any number of properties and metadata.

When a button is clicked the metadata for that button is attached to the registered callback function.

## Quick start

1. Start the `flicd` deamon.
1. Run `node index.js`
1. Edit the created `buttons.json` file and add some metadata. 
   ```
   {
     "80:e4:da:11:11:1d": {
       "name": "Button One"
     },
     "80:e4:da:22:22:2d": {
       "name": "Button Two"
     }
   }
   ```
1. Restart `node index.js`
