var PostMessagePlugin = {
  SendGameOver: function() {
    window.postMessage("GAME_OVER", "*");
    console.log("[PostMessagePlugin] Sent GAME_OVER to Flutter.");

    window.parent.postMessage("GAME_OVER", "*");
    console.log("[PostMessagePlugin] Sent GAME_OVER to Web.");
  }
};

mergeInto(LibraryManager.library, PostMessagePlugin);
