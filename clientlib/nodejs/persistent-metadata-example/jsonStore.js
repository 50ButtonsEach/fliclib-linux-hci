const fs = require('fs')

const JSONStore = (filename) => {
		const get = () => new Promise((resolve, reject) => {
			try {
				resolve(JSON.parse(fs.readFileSync(filename), 'utf8'))
			} catch (e) {
				if (e.code === 'ENOENT') {
					resolve({})
				} else {
					reject(e)
				}
			}
		})

		const save = (data) => new Promise((resolve, reject) => {
			try {
				fs.writeFileSync(filename, JSON.stringify(data, null, 4), 'utf8')
				resolve()
			} catch (e) {
				reject(e)
			}
		})

    return {
      get,
      save
    }
}

module.exports = JSONStore
