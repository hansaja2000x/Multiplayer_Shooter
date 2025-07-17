var SocketPlugin = {

    $Data: {
        SocketGameObjectName: "",
        sockets: new Map(),
        //send to unity stuff
        CallUnityEvent: function(id, event, data) {
            var JsonData = null;
            if (data != null) {
                JsonData = data;
            }
            unityInstance.SendMessage(Data.SocketGameObjectName, 'callSocketEvent', JSON.stringify({
                EventName: event,
                SocketId: id,
                JsonData: JsonData
            }));
        },
    },

    SetupGameObjectName: function(str) {
        Data.SocketGameObjectName = UTF8ToString(str);
        Data.sockets = new Map();
    },

    GetProtocol: function() {
        if (typeof io !== "undefined")
            return io.getProtocol;
        else {
            console.error("SocketIO io object not found! Did you forget to include Reference in header?");
            throw new Error("SocketIO object not found! Did you forget to include Reference in header?");
        }
    },

    EstablishSocket: function(url_raw, options_raw) {
        if (typeof io !== "undefined") {
            const url = UTF8ToString(url_raw);
            const options = UTF8ToString(options_raw);

            var soc;
            if (options.length > 0)
                soc = io(url, JSON.parse(options));
            else
                soc = io(url);

            var id = 0;
            do {
                id = Math.floor(Math.random() * 10000) + 1;
            } while (Data.sockets.has(id));

            Data.sockets.set(id, soc);

            soc.onAny(function(event, args) {
                Data.CallUnityEvent(id, event, args);
            });

            return id;
        } else {
            console.error("SocketIO io object not found! Did you forget to include Reference in header?");
            throw new Error("SocketIO object not found! Did you forget to include Reference in header?");
        }
    },

    Socket_IsConnected: function(id) {
        return Data.sockets.get(id).connected;
    },

    Socket_Connect: function(id) {
        Data.sockets.get(id).connect();
    },

    Socket_Disconnect: function(id) {
        Data.sockets.get(id).disconnect();
    },

    Socket_Emit: function(id, event_raw, data_raw) {
        const eventName = UTF8ToString(event_raw);
        const payload = UTF8ToString(data_raw);
        if (payload.length == 0) {
            Data.sockets.get(id).emit(eventName, null);
        } else {
            Data.sockets.get(id).emit(eventName, payload);
        }
    },

    Socket_Get_Conn_Id: function(id) {
        var result = Data.sockets.get(id).id;
        if (result !== undefined) {
            var bufferSize = lengthBytesUTF8(result) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(result, buffer, bufferSize);
            return buffer;
        } else {
            return null;
        }
    },
};

autoAddDeps(SocketPlugin, "$Data");
mergeInto(LibraryManager.library, SocketPlugin);
