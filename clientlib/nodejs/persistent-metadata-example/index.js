const flicConnector = require('./flic')

const flic = flicConnector({
	defaultButtonProps: {
		name: "UNNAMED"
	}
})

flic.on('ButtonSingleClick', (type, wasQueued, timeDiff, metadata) => {
	console.log('Click detected on button with metadata', metadata)
})

flic.on('ButtonHold', (type, wasQueued, timediff, metadata) => {
	console.log('Hold detected on button with metadata', metadata)
})

console.log('Listening for single click and button hold')

