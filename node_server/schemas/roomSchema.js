const mongoose = require('mongoose');
const { Schema } = mongoose;

const playerSchema = new Schema({
  name: { type: String }, 
  uuid: { type: String }, 
  profileImage: { type: String, required: false },
  ready: { type: Boolean, default: false },
  status: { type: String, enum: ['WON', 'DEFEATED', 'DROPPED', 'TIMEOUT'], required: false } // Added player status
});

const roomSchema = new Schema({
  gameSessionUuid: { type: String, required: true, unique: true },
  players: { type: [playerSchema], default: [] },
  createdDate: { type: Date, default: Date.now },
  gameEndTime: { type: Date, required: false }, // Added game end time
  roundWins: { type: Map, of: Number, default: {} }, // Added round wins per player UUID
  isDraw: { type: Boolean, default: false }, // Added draw status
  remainingTime: { type: Number, required: false } // Added remaining time in seconds
});

roomSchema.index({ gameSessionUuid: 1 }, { unique: true });

const Room = mongoose.model('Room', roomSchema);
module.exports = Room;