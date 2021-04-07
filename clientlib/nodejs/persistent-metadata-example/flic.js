const flicLib = require('../fliclibNodeJs')
const JSONStore = require('./jsonStore')

const Flic = (opt) => {
	const options = Object.assign({
		dbFile: 'buttons.json',
		defaultButtonProps: {
			name: "UNNAMED"
		}
	}, opt)

	const state = {
		buttons: {},
		eventListeners: {}
	}

	const client = new flicLib.FlicClient('localhost', 5551)

	const db = JSONStore(options.dbFile)

	const mergeButtons = (defaultButtonProps) => (buttons, buttonId) => {
		buttons[buttonId] = Object.assign({}, defaultButtonProps, buttons[buttonId])
		return buttons
	}

	const getNewAndOldButtons = (buttonIds, defaultButtonProps) => (storedButtons) => {
		return buttonIds.reduce(mergeButtons(defaultButtonProps), storedButtons)
	}

	const updateState = (key) => (value) => {
		state[key] = value
		return value
	}

	const addEventListener = (buttonId) => {
		const cc = new flicLib.FlicConnectionChannel(buttonId)
		client.addConnectionChannel(cc)
		cc.on('buttonSingleOrDoubleClickOrHold', (clickType, wasQueued, timeDiff) => {
			if (state.eventListeners[clickType]) {
				state.eventListeners[clickType].forEach((callback) => {
					callback(clickType, wasQueued, timeDiff, state.buttons[buttonId])
				})
			}
		})
	}

	const addEventListenerToButtons = () => {
		Object.keys(state.buttons).forEach(addEventListener)
	}

	client.once('ready', () => {
		client.getInfo(info => {
			db.get()
				.then(getNewAndOldButtons(info.bdAddrOfVerifiedButtons, options.defaultButtonProps))
				.then(updateState('buttons'))
				.then(db.save)
				.then(addEventListenerToButtons)
		})
	})

	const on = (type, callback) => {
		if (!state.eventListeners[type]) {
			state.eventListeners[type] = []
		}
		state.eventListeners[type].push(callback)
	}

	return {
		on
	}
}

module.exports = Flic
