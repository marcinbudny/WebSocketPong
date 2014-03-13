function Pong(canvasSelector, messageSelector, scoreSelector, applicationUrl) {
    
    this.canvas = $(canvasSelector)[0];
    this.messageSelector = messageSelector;
    this.scoreSelector = scoreSelector;

    this.playerHeight = 50;
    this.playerWidth = 6;
    this.ballRadius = 3;
    this.score = [0, 0];

    // which player am I?
    this.myPlayer = -1;

    this.players = [
    {
        color: "red",
        xPos: 5 + this.playerWidth / 2,
        yPos: this.canvas.height / 2 
    },
    {
        color: "blue",
        xPos: this.canvas.width - 5 - this.playerWidth / 2,
        yPos: this.canvas.height / 2,
    }
    ];

    this.ball = {
        color: "black",
        xPos: this.canvas.width / 2,
        yPos: this.canvas.height / 2
    };

    // register on mousemove over canvas
    $(canvasSelector).mousemove($.proxy(function (e) {
        if (this.myPlayer == -1)
            return;

        // calulate mouse vertical position relative to canvas
        var offset = $(this.canvas).offset();
        var mouseY = e.pageY - offset.top;

        // constraint movement
        if (mouseY < this.playerHeight / 2)
            mouseY = this.playerHeight / 2;
        if (mouseY > this.canvas.height - this.playerHeight / 2)
            mouseY = this.canvas.height - this.playerHeight / 2;

        this.players[this.myPlayer].yPos = mouseY;

        // send message to server with new position
        this.sendMessage({ YPos: mouseY });

        this.draw();
    }, this));

    this.draw();
    this.displayScore();

    // start connecting websockets
    // Firefox 6 and 7 requires Moz prefix...
    if(typeof(MozWebSocket) == "function") {
        this.socket = new MozWebSocket(applicationUrl);
        this.openStateConst = MozWebSocket.OPEN;
    } else {
        this.socket = new WebSocket(applicationUrl);
        this.openStateConst = WebSocket.OPEN;
    }

    // register to socket events
    this.socket.onopen = $.proxy(function () {
        this.info("You are connected...");
    }, this);
    this.socket.onclose = $.proxy(function () {
        this.info("Other player disconnected! Refresh page to restart game");
    }, this);
    this.socket.onerror = $.proxy(function () {
        this.info("You have been disconnected! Sorry for that, refresh page to restart game");
    }, this);
    this.socket.onmessage = $.proxy(function (msg) {
        this.processMessage(msg);
    }, this);
}

Pong.prototype = {
    initialize : function() {
        
    },

    // draws players and ball
    draw : function () {
        var ctx = this.canvas.getContext('2d');

        ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);

        for (var i in this.players) {
            ctx.beginPath();
            var player = this.players[i];
            ctx.rect(player.xPos - this.playerWidth / 2, player.yPos - this.playerHeight / 2, this.playerWidth, this.playerHeight);
            ctx.fillStyle = player.color;
            ctx.fill();
        }

        ctx.beginPath();
        ctx.arc(this.ball.xPos /*- this.ballRadius*/, this.ball.yPos /*- this.ballRadius*/, this.ballRadius, 0, 2 * Math.PI);
        ctx.fillStyle = this.ball.color;
        ctx.fill();

    },

    // displays info message for user
    info: function (text) {
        $(this.messageSelector).html(text);
    },

    // dislay current score
    displayScore: function () {
        $(this.scoreSelector).html("The score is " + this.score[0] + ":" + this.score[1]);
    },

    // gets the other player
    otherPlayer: function() {
        return this.players[this.myPlayer == 0 ? 1 : 0];
    },

    // serializes and sends message to server
    sendMessage: function (msg) {
        if (this.socket != "undefined" && this.socket.readyState == this.openStateConst) {
            var msgText = JSON.stringify(msg);
            this.socket.send(msgText);
        }
    },

    // processes message from server basing on its type
    processMessage: function (msg) {
        var data = JSON.parse(msg.data);

        switch (data.Type) {
            case "PlayerNumberMessage":
                this.myPlayer = data.PlayerNumber;
                if (this.myPlayer == 0) {
                    this.info("<span style='color:red'>You are red.</span>");
                } else {
                    this.info("<span style='color:blue'>You are blue.</span>");
                }
                break;

            case "PlayerPositionMessage":
                this.otherPlayer().yPos = data.YPos;
                this.draw();
                break;

            case "BallPositionMessage":
                this.ball.xPos = data.XPos;
                this.ball.yPos = data.YPos;
                this.draw();
                break;

            case "ScoreMessage":
                this.score = data.Score;
                this.displayScore();
                break;
        }
    }


};

