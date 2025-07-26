var PostMessagePlugin = {
  SendGameOver: function() {
    window.postMessage("GAME_OVER", "*");
    window.parent.postMessage('GAME_OVER','*');
  }
};
mergeInto(LibraryManager.library, PostMessagePlugin);