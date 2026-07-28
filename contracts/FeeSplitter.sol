// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/// @title  FeeSplitter — one-transaction ETH send with a platform fee
/// @notice Forwards an ETH payment to a recipient and the developer fee to a fixed address, in a
///         SINGLE transaction, so the fee costs no extra gas beyond the split. The wallet decides
///         the split (fee-on-top: it sends `recipientAmount + fee` as msg.value); the contract just
///         forwards both legs atomically and reverts if either fails.
///
///         Non-custodial by construction: it never keeps funds. Every wei received in a call is
///         forwarded within that same call, or the whole transaction reverts. There is no owner,
///         no withdraw, no upgrade — nothing that can trap or divert funds later.
contract FeeSplitter {
    /// @notice The developer fee recipient. Immutable — set once at deployment, can never change.
    address payable public immutable feeRecipient;

    /// @notice Upper bound the wallet's fee is expected to stay within (basis points; 200 = 2%).
    ///         Enforced so a buggy/hostile caller cannot over-skim: fee may not exceed this of the
    ///         total. The recipient always receives at least (10000 - maxFeeBps)/10000 of msg.value.
    uint16 public immutable maxFeeBps;

    event Paid(address indexed recipient, uint256 recipientAmount, uint256 fee);

    error BadInput();
    error FeeTooHigh();
    error TransferFailed();

    constructor(address payable _feeRecipient, uint16 _maxFeeBps) {
        if (_feeRecipient == address(0) || _maxFeeBps > 200) revert BadInput();
        feeRecipient = _feeRecipient;
        maxFeeBps = _maxFeeBps;
    }

    /// @notice Send `recipientAmount` to `recipient`; the remainder of msg.value is the fee.
    /// @param  recipient        who receives the payment
    /// @param  recipientAmount  exactly how much the recipient gets; msg.value - this = the fee
    function pay(address payable recipient, uint256 recipientAmount) external payable {
        if (recipient == address(0) || recipientAmount == 0 || recipientAmount > msg.value) {
            revert BadInput();
        }

        uint256 fee = msg.value - recipientAmount;
        // Guard: the fee can never exceed maxFeeBps of the total sent.
        if (fee * 10000 > uint256(maxFeeBps) * msg.value) revert FeeTooHigh();

        (bool okRecipient, ) = recipient.call{value: recipientAmount}("");
        if (!okRecipient) revert TransferFailed();

        if (fee > 0) {
            (bool okFee, ) = feeRecipient.call{value: fee}("");
            if (!okFee) revert TransferFailed();
        }

        emit Paid(recipient, recipientAmount, fee);
    }
}
