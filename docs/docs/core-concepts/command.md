---
sidebar_position: 3
---

# Command

## Overview
The `Command<'msg, 'sharedMsg>` msg type in Elmish Land is an abstraction built on top of [Elmish's standard Cmd\<'msg\> type](https://elmish.github.io/elmish/#commands). The `Command` module contians convenience functions for using Elmish Cmd:s and functions to allow communication from pages to the Shared module.

## Command.none
Similar to Cmd.none, this tells Elmish Land not to run any commands.

## Command.batch
Similar to Cmd.batch, this allows you to send many commands at once.

## Command.ofCmd
Convert an Elmish Cmd to an Elmish Land Command.

## Command.ofMsg
Send a msg as a Command.

## Command.ofShared
Send a SharedMsg from a page or layout.

## Command.ofPromise
Run a promise that will send the specified message when completed. Throws exception if it fails.

## Command.tryOfPromise
Run a promise that will send the specified messages when succeeds or fails. Does not throw exceptions.