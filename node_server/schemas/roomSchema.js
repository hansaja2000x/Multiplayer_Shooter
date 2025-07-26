const mongoose = require('mongoose');
const { Schema } = mongoose;

const playerSchema = new Schema({
  name: { type: String, required: true },
  uuid: { type: String, required: true },
  profileImage: { type: String, required: false },
  ready: { type: Boolean, default: false },
});

const roomSchema = new Schema({
  gameSessionUuid: { type: String, required: true, unique: true },
  players: { type: [playerSchema], default: [] },
  createdDate: { type: Date, default: Date.now },
});

roomSchema.index({ gameSessionUuid: 1 }, { unique: true });

const Room = mongoose.model('Room', roomSchema);
module.exports = Room;

